using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Modules.History.View;
using RimAI.Core.Contracts.Config;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
    internal sealed class GroupChatAct : IStageAct
    {
        private readonly ILLMService _llm;
        private readonly IWorldActionService _worldAction;
        private readonly IPromptService _prompt;
        private readonly ConfigurationService _cfg;
        private readonly IWorldDataService _worldData;
        private readonly IDisplayNameService _display;

        public GroupChatAct(ILLMService llm, IWorldActionService worldAction = null, IPromptService prompt = null, IConfigurationService cfg = null, IWorldDataService worldData = null)
        {
            _llm = llm; _worldAction = worldAction; _prompt = prompt; _cfg = cfg as ConfigurationService; _worldData = worldData;
            _display = new DisplayNameAdapter(worldData);
        }

        public string Name => "GroupChat";

        public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
        public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

        public bool IsEligible(StageExecutionRequest req)
        {
            return req?.Ticket != null && !string.IsNullOrWhiteSpace(req.Ticket.ConvKey);
        }

        public async Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct)
        {
            var conv = req?.Ticket?.ConvKey ?? ("agent:stage|" + (DateTime.UtcNow.Ticks));
            var participants = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).ToList();
            // 解析轮数（从 ScenarioText 提示里读不到就用配置默认的 1）
            int rounds = 1;
            try
            {
                if (!string.IsNullOrWhiteSpace(req?.ScenarioText))
                {
                    // 尝试从“预期轮数=数字”中提取
                    var idx = req.ScenarioText.IndexOf("预期轮数=");
                    if (idx >= 0)
                    {
                        var tail = req.ScenarioText.Substring(idx + 5);
                        var numStr = new string(tail.TakeWhile(ch => char.IsDigit(ch)).ToArray());
                        if (int.TryParse(numStr, out var n)) rounds = Math.Max(1, Math.Min(3, n));
                    }
                }
            }
            catch { rounds = 1; }

            try { rounds = Math.Max(1, Math.Min(3, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.Rounds ?? rounds)); } catch { }

            // 启动“群聊任务”：移动并下发 Wait；会话最长 60s（可配置）
            GroupChatSessionHandle session = null;
            int initiatorId = -1;
            var others = new List<int>();
            try
            {
                var first = participants.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first) && first.StartsWith("pawn:"))
                {
                    int.TryParse(first.Substring(5), out initiatorId);
                }
                foreach (var p in participants.Skip(1))
                {
                    if (p != null && p.StartsWith("pawn:")) { if (int.TryParse(p.Substring(5), out var vid)) others.Add(vid); }
                }
                var radius = Math.Max(1, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.GatherRadius ?? 3);
                var maxDur = TimeSpan.FromMilliseconds(Math.Max(1000, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.MaxSessionDurationMs ?? 60000));
                if (_worldAction != null && initiatorId > 0 && others.Count > 0)
                {
                    session = await _worldAction.StartGroupChatDutyAsync(initiatorId, others, radius, maxDur, ct).ConfigureAwait(false);
                    if (session == null) { return new ActResult { Completed = false, Reason = "WorldActionFailed", FinalText = "（无法开始群聊任务）" }; }
                }
            }
            catch { }

            int actualRounds = 0;
            var bubbleDelayMs = Math.Max(0, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.BubbleDelayMs ?? 1000);
            var rnd = new Random(unchecked(Environment.TickCount ^ conv.GetHashCode()));

            string finalTranscript = string.Empty;
            var transcript = new System.Text.StringBuilder();

            // 预取：在播放本轮气泡时并发请求下一轮
            Func<int, CancellationToken, Task<string>> startRequest = async (round, token) =>
            {
                // 组装提示词（P11）：Stage Scope + 参与者
                string systemLocal;
                try
                {
                    if (_prompt != null)
                    {
                        var reqPrompt = new PromptBuildRequest { Scope = PromptScope.Stage, ConvKey = conv, ParticipantIds = participants, Locale = req?.Locale };
                        var built = await _prompt.BuildAsync(reqPrompt, token).ConfigureAwait(false);
                        systemLocal = built?.SystemPrompt ?? string.Empty;
                    }
                    else { systemLocal = string.Empty; }
                }
                catch { systemLocal = string.Empty; }

                var userLocal = BuildUserPromptSimpleMap(participants, round);
                var chatReqLocal = new RimAI.Framework.Contracts.UnifiedChatRequest
                {
                    ConversationId = conv,
                    Messages = new List<RimAI.Framework.Contracts.ChatMessage>
                    {
                        new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=systemLocal },
                        new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=userLocal }
                    },
                    Stream = false,
                    ForceJsonOutput = true
                };
                var respLocal = await _llm.GetResponseAsync(chatReqLocal, token).ConfigureAwait(false);
                if (!respLocal.IsSuccess) return null;
                return respLocal.Value?.Message?.Content ?? string.Empty;
            };

            Task<string> currentTask = startRequest(1, ct);
            bool aborted = false;
            for (int r = 1; r <= rounds && !ct.IsCancellationRequested; r++)
            {
                if (session != null && !_worldAction.IsGroupChatSessionAlive(session)) { aborted = true; break; }
                var json = await currentTask.ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json)) { aborted = true; break; }

                // 提前预取下一轮
                Task<string> nextTask = null;
                if (r < rounds)
                {
                    nextTask = startRequest(r + 1, ct);
                }

                try
                {
                    var shaped = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (shaped != null && shaped.Count > 0)
                    {
                        actualRounds++;
                        // 随机顺序
                        var items = shaped.ToList();
                        items.Sort((a,b) => rnd.Next(-1,2));

                        // 解析显示名映射（pawn:<id> -> name）
                        var nameMap = new Dictionary<string,string>();
                        try
                        {
                            var ids = participants.Where(x => x != null && x.StartsWith("pawn:")).ToList();
                            foreach (var id in ids)
                            {
                                try
                                {
                                    var s = id.Substring(5);
                                    if (int.TryParse(s, out var pid))
                                    {
                                        var snap = await _worldData.GetPawnPromptSnapshotAsync(pid, ct).ConfigureAwait(false);
                                        var name = snap?.Id?.Name;
                                        if (!string.IsNullOrWhiteSpace(name)) nameMap[id] = name; else nameMap[id] = id;
                                    }
                                }
                                catch { nameMap[id] = id; }
                            }
                        }
                        catch { }

                        // 播放气泡并拼接文本（禁止 JSON 写入历史）
                        transcript.AppendLine($"第{r}轮");
                        foreach (var kv in items)
                        {
                            var key = kv.Key?.Trim();
                            var text = kv.Value?.Trim();
                            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(text)) continue;
                            // key 形如 "1".."N"；按参与者序列映射到具体 pawn
                            int idx;
                            if (!int.TryParse(key, out idx)) idx = 1;
                            idx = Math.Max(1, Math.Min(participants.Count, idx));
                            var pidStr = participants[idx - 1];
                            int pid;
                            if (!string.IsNullOrWhiteSpace(pidStr) && pidStr.StartsWith("pawn:") && int.TryParse(pidStr.Substring(5), out pid))
                            {
                                try { await _worldAction.ShowSpeechTextAsync(pid, text, ct).ConfigureAwait(false); } catch { }
                                try { await Task.Delay(bubbleDelayMs, ct).ConfigureAwait(false); } catch { }
                            }
                            var disp = nameMap.TryGetValue(pidStr, out var nm) ? nm : pidStr;
                            transcript.AppendLine($"【{disp}】{text}");
                        }
                        transcript.AppendLine();
                    }
                }
                catch { aborted = true; break; }

                // 轮次间隔（0-1 秒）
                var minMs = Math.Max(0, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.RoundIntervalMinMs ?? 0);
                var maxMs = Math.Max(minMs, _cfg?.GetInternal()?.Stage?.Acts?.GroupChat?.RoundIntervalMaxMs ?? 1000);
                var delay = (minMs == maxMs) ? minMs : (minMs + rnd.Next(0, (maxMs - minMs + 1)));
                if (delay > 0)
                {
                    try { await Task.Delay(delay, ct).ConfigureAwait(false); } catch { }
                }
                if (session != null && !_worldAction.IsGroupChatSessionAlive(session)) { aborted = true; break; }
                if (nextTask == null) break;
                currentTask = nextTask;
            }

            // 结束世界会话
            try { if (session != null) await _worldAction.EndGroupChatDutyAsync(session, (!aborted && actualRounds >= rounds) ? "Completed" : "Aborted", ct).ConfigureAwait(false); } catch { }

            if (aborted || actualRounds < rounds)
            {
                return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）", Rounds = 0 };
            }
            finalTranscript = transcript.ToString().TrimEnd();
            return new ActResult { Completed = true, Reason = "Completed", FinalText = finalTranscript, Rounds = actualRounds };
        }

        private static string BuildUserPromptSimpleMap(List<string> participants, int round)
        {
            // JSON 模板固定：{"1":"...","2":"...",...}，按参与者顺序映射 1..N；不要输出解释文本
            var mapKeys = new List<string>();
            for (int i = 0; i < participants.Count; i++) mapKeys.Add($"\"{i+1}\":\"...\"");
            var template = "{" + string.Join(",", mapKeys) + "}";
            return $"现在，生成第{round}轮对话。请严格输出 JSON，键为 1..{participants.Count}，值为对应角色的台词（简短口语化），不得包含解释文本：{template}";
        }
        
    }
}


