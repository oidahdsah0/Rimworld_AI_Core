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
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Contracts.Config;
using Verse;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
    internal sealed class GroupChatAct : IStageAct, IAutoStageIntentProvider
    {
        private readonly ILLMService _llm;
        private readonly IWorldActionService _worldAction;
        private readonly IPromptService _prompt;
        private readonly ConfigurationService _cfg;
        private readonly IWorldDataService _worldData;
        private readonly IDisplayNameService _display;
        private readonly RimAI.Core.Source.Modules.History.IHistoryService _history;
        private readonly ILocalizationService _loc;

        public GroupChatAct(ILLMService llm, IWorldActionService worldAction = null, IPromptService prompt = null, IConfigurationService cfg = null, IWorldDataService worldData = null, ILocalizationService loc = null, RimAI.Core.Source.Modules.History.IHistoryService history = null)
        {
            _llm = llm; _worldAction = worldAction; _prompt = prompt; _cfg = cfg as ConfigurationService; _worldData = worldData; _loc = loc; _history = history;
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
            // 强制将群聊写入 Stage 专属会话，避免污染玩家-小人对话历史
            var conv = req?.Ticket?.ConvKey;
            if (string.IsNullOrWhiteSpace(conv) || !conv.StartsWith("agent:stage|", StringComparison.Ordinal))
            {
                string participantsKey = string.Join("|", (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x, StringComparer.Ordinal));
                var seed = req?.Seed ?? DateTime.UtcNow.Ticks.ToString();
                conv = string.IsNullOrWhiteSpace(participantsKey)
                    ? ($"agent:stage|group|{seed}")
                    : ($"agent:stage|group|{participantsKey}|{seed}");
            }
            var participants = (req?.Ticket?.ParticipantIds ?? Array.Empty<string>()).ToList();
            if (participants.Count < 2)
            {
                var msgFew = "RimAI.Stage.GroupChat.TooFewParticipants".Translate().ToString();
                return new ActResult { Completed = false, Reason = "TooFewParticipants", FinalText = msgFew };
            }
            // 确保历史服务已登记该会话的参与者映射，便于“关联对话”查询
            try { if (_history != null) await _history.UpsertParticipantsAsync(conv, participants, ct).ConfigureAwait(false); } catch { }
            // 解析轮数：优先 ScenarioText；否则随机 1..3
            int rounds = 1;
            try
            {
                if (!string.IsNullOrWhiteSpace(req?.ScenarioText))
                {
                    var idx = req.ScenarioText.IndexOf("预期轮数=");
                    if (idx >= 0)
                    {
                        var tail = req.ScenarioText.Substring(idx + 5);
                        var numStr = new string(tail.TakeWhile(ch => char.IsDigit(ch)).ToArray());
                        if (int.TryParse(numStr, out var n)) rounds = Math.Max(1, Math.Min(3, n));
                    }
                    else { rounds = 1 + new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, 3); }
                }
                else { rounds = 1 + new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, 3); }
            }
            catch { rounds = 1 + new Random(unchecked(Environment.TickCount ^ conv.GetHashCode())).Next(0, 3); }

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
                    if (session == null) { var failMsg = "RimAI.Stage.GroupChat.TaskStartFailed".Translate().ToString(); return new ActResult { Completed = false, Reason = "WorldActionFailed", FinalText = failMsg }; }
                }
            }
            catch (Exception ex) { try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"startSessionError:{ex.GetType().Name}", false, ct).ConfigureAwait(false); } catch { } }

            int actualRounds = 0;
            var rnd = new Random(unchecked(Environment.TickCount ^ conv.GetHashCode()));

            string finalTranscript = string.Empty;
            var transcript = new System.Text.StringBuilder();
            // 为后续轮次构建“前几轮对白”的 assist 消息块（每轮一个块，内部按“姓名: 台词”逐行）
            var priorRoundAssistantBlocks = new List<string>();

            // 生产-消费模型：将所有轮次消息放入 FIFO 队列，按 1 秒间隔出队
            var outQueue = new System.Collections.Concurrent.ConcurrentQueue<(string speaker, string content, bool end)>();
            var actStartUtc = DateTime.UtcNow; // Act 现实时间兜底计时
            // 动态时间窗：允许多轮完成（至少 20s，或按轮数放宽）
            var hardTimeout = TimeSpan.FromSeconds(Math.Max(20, rounds * 8));
            var enqueueDeadlineUtc = actStartUtc.Add(hardTimeout);
            // 硬兜底计时器：达到时间窗立即取消
            using var hardCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            hardCts.CancelAfter(hardTimeout);
            var hard = hardCts.Token;

            // 统一结束清理（Act 内兜底）：结束世界会话 + 可选历史记录
            async Task TryEndSessionAsync(string reason, bool completed)
            {
                try
                {
                    if (session != null)
                    {
                        await _worldAction.EndGroupChatDutyAsync(session, completed ? "Completed" : (string.IsNullOrWhiteSpace(reason) ? "Aborted" : reason), ct).ConfigureAwait(false);
                    }
                }
                catch { }
                try
                {
                    if (_history != null)
                    {
                        // 在日志记录之前，显式写入一条用户可见的结束语句
                        try { var endMsg = "RimAI.Stage.GroupChat.RoundEnd".Translate().ToString(); await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "chat", endMsg, false, ct).ConfigureAwait(false); } catch { }
                        await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"end:{(completed ? "Completed" : reason ?? "Aborted")}", false, ct).ConfigureAwait(false);
                    }
                }
                catch { }
                // 左上角提示（Act 结束）
                try { var endUi = "RimAI.Stage.GroupChat.RoundEnd".Translate().ToString(); await _worldAction.ShowTopLeftMessageAsync(endUi, RimWorld.MessageTypeDefOf.TaskCompletion, ct).ConfigureAwait(false); } catch { }
            }

            // 预取：在播放本轮气泡时并发请求下一轮
            Func<int, CancellationToken, Task<string>> startRequest = async (round, token) =>
            {
                // 使用 P11 Prompting（Stage Scope）统一构建系统提示（包含参与者白名单与 JSON 数组合约）
                string locale = req?.Locale;
                string systemLocal = string.Empty;
                try
                {
                    if (_prompt != null)
                    {
                        var reqPrompt = new PromptBuildRequest { Scope = PromptScope.Stage, ConvKey = conv, ParticipantIds = participants, Locale = req?.Locale };
                        var built = await _prompt.BuildAsync(reqPrompt, token).ConfigureAwait(false);
                        systemLocal = built?.SystemPrompt ?? string.Empty;
                    }
                }
                catch { systemLocal = string.Empty; }
                if (string.IsNullOrWhiteSpace(systemLocal))
                {
                    // 兜底：最小合约行（含白名单），防止缺本地化/作曲器失败
                    var whitelist = string.Join(", ", participants.Select((id, i) => $"{i + 1}:{id}"));
                    systemLocal = _loc?.Format(req?.Locale ?? "en", "stage.groupchat.system_fallback", new Dictionary<string,string>{{"whitelist", whitelist}},
                        $"Output strictly a JSON array: each element is {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}; speakers must be in whitelist: [{whitelist}]; no extra explanations.")
                        ?? $"Output strictly a JSON array: each element is {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}; speakers must be in whitelist: [{whitelist}]; no extra explanations.";
                }

                // 用户段提示：本地化 + 重复关键 JSON 合约与白名单，强化模型遵循度
                var whitelistForUser = string.Join(", ", participants.Select((id, i) => $"{i + 1}:{id}"));
                string userLocal;
                try
                {
                    var args = new Dictionary<string, string> { { "round", round.ToString() }, { "whitelist", whitelistForUser } };
                        userLocal = _loc?.Format(locale ?? "en", "stage.groupchat.user", args,
                            $"Now produce round {round} of the group chat. Output JSON array only: each element is {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}; speakers must be in whitelist: [{whitelistForUser}]; no extra explanations.")
                            ?? $"Now produce round {round} of the group chat. Output JSON array only: each element is {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}; speakers must be in whitelist: [{whitelistForUser}]; no extra explanations.";
                }
                catch
                {
                        userLocal = $"Now produce round {round} of the group chat. Output JSON array only: each element is {{\"speaker\":\"pawn:<id>\",\"content\":\"...\"}}; speakers must be in whitelist: [{whitelistForUser}]; no extra explanations.";
                }
                var msgs = new List<RimAI.Framework.Contracts.ChatMessage>();
                msgs.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = systemLocal });
                // 将前几轮对白作为 assist 块加入（每轮一个消息，内部是多行“姓名: 台词”）
                if (priorRoundAssistantBlocks.Count > 0)
                {
                    foreach (var block in priorRoundAssistantBlocks)
                    {
                        if (!string.IsNullOrWhiteSpace(block))
                        {
                            msgs.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "assistant", Content = block });
                        }
                    }
                }
                msgs.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "user", Content = userLocal });
                var chatReqLocal = new RimAI.Framework.Contracts.UnifiedChatRequest
                {
                    ConversationId = conv,
                    Messages = msgs,
                    Stream = false,
                    ForceJsonOutput = true
                };
                try { await _worldAction.ShowTopLeftMessageAsync("RimAI.Stage.GroupChat.RoundBegin".Translate(round).ToString(), RimWorld.MessageTypeDefOf.NeutralEvent, token).ConfigureAwait(false); } catch { }
                var respLocal = await _llm.GetResponseAsync(chatReqLocal, token).ConfigureAwait(false);
                // 控制台输出原始 LLM 返回内容（截断以防日志过长）
                try
                {
                    var raw = respLocal?.Value?.Message?.Content ?? string.Empty;
                    if (raw.Length > 4000) raw = raw.Substring(0, 4000) + "...";
                    Verse.Log.Message($"[RimAI.Core][P9] GroupChat LLM raw (round={round})\nconv={conv}\n{raw}");
                }
                catch { }
                if (!respLocal.IsSuccess) return null;
                return respLocal.Value?.Message?.Content ?? string.Empty;
            };

            // 启动消费者：每 1.5 秒出队 1 条，遇到结束标记则停止
            var consumeTask = Task.Run(async () =>
            {
                while (!hard.IsCancellationRequested)
                {
                    if (outQueue.TryDequeue(out var item))
                    {
                        if (item.end) break;
                        var pidStr = item.speaker;
                        var text = item.content;
                        int pid;
                        if (!string.IsNullOrWhiteSpace(pidStr) && pidStr.StartsWith("pawn:") && int.TryParse(pidStr.Substring(5), out pid))
                        {
                            try { await _worldAction.ShowSpeechTextAsync(pid, text, hard).ConfigureAwait(false); } catch { }
                        }
                        try { await Task.Delay(1500, hard).ConfigureAwait(false); } catch { break; }
                    }
                    else
                    {
                        try { await Task.Delay(100, hard).ConfigureAwait(false); } catch { break; }
                    }
                }
                // drain: 如果结束标记先到，但队列里还有残余（理论不应发生），尽量清空避免下一轮串扰
                while (outQueue.TryDequeue(out _)) { }
            }, hard);

            // 生产者：顺序请求每一轮，将消息随机顺序入队；超出 10 秒门限直接入队结束标记
            bool aborted = false;
            bool deadlineHit = false;
            for (int r = 1; r <= rounds && !hard.IsCancellationRequested; r++)
            {
                if (session != null && !_worldAction.IsGroupChatSessionAlive(session)) { aborted = true; break; }
                var requestStartUtc = DateTime.UtcNow;
                string json = null;
                try { json = await startRequest(r, hard).ConfigureAwait(false); }
                catch (OperationCanceledException) { aborted = true; break; }
                if (string.IsNullOrWhiteSpace(json)) { aborted = true; break; }

                try
                {
                    var arr = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json);
                    var messages = new List<(string speaker, string content)>();
                    if (arr != null && arr.Count > 0)
                    {
                        foreach (var obj in arr)
                        {
                            if (obj == null) continue;
                            object spkObj; object txtObj;
                            var hasSpk = obj.TryGetValue("speaker", out spkObj);
                            var hasTxt = obj.TryGetValue("content", out txtObj);
                            var spk = hasSpk ? spkObj?.ToString() : null;
                            var txt = hasTxt ? txtObj?.ToString() : null;
                            if (string.IsNullOrWhiteSpace(spk) || string.IsNullOrWhiteSpace(txt)) continue;
                            if (!participants.Contains(spk)) continue;
                            messages.Add((spk.Trim(), txt.Trim()));
                        }
                    }

                    if (messages.Count > 0)
                    {
                        actualRounds++;
                        // 打乱顺序后入队：若本轮请求在门限之前发起（requestStartUtc <= deadline），保证整批消息完整入队
                        bool allowBatchBeyondDeadline = requestStartUtc <= enqueueDeadlineUtc;
                        var shuffled = messages.OrderBy(_ => rnd.Next()).ToList();
                        foreach (var msg in shuffled)
                        {
                            if (!allowBatchBeyondDeadline && DateTime.UtcNow > enqueueDeadlineUtc) { deadlineHit = true; break; }
                            outQueue.Enqueue((msg.speaker, msg.content, false));
                            // 同步构建简单文本记录（用于结果文本）
                            transcript.AppendLine($"【{msg.speaker}】{msg.content}");
                            try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", msg.speaker, "chat", msg.content, advanceTurn: false, ct: hard).ConfigureAwait(false); } catch { }
                        }
                        transcript.AppendLine();
                        // 为下一轮构建 assist 块：使用上一轮的原始 JSON 数组，强化模型对 JSON 输出的遵循
                        try
                        {
                            var jsonItems = new List<object>();
                            foreach (var msg in shuffled)
                            {
                                jsonItems.Add(new { speaker = msg.speaker, content = msg.content });
                            }
                            var jsonBlock = JsonConvert.SerializeObject(jsonItems);
                            if (!string.IsNullOrWhiteSpace(jsonBlock)) priorRoundAssistantBlocks.Add(jsonBlock);
                        }
                        catch { }
                    }
                    else { aborted = true; break; }
                }
                catch (Exception ex) { aborted = true; try { if (_history != null) await _history.AppendRecordAsync(conv, $"Stage:{Name}", "agent:stage", "log", $"parseError:{ex.GetType().Name}", false, hard).ConfigureAwait(false); } catch { } break; }

                // 轮次间隔固定 1.5 秒
                if (deadlineHit) { break; }
                try { await Task.Delay(1500, hard).ConfigureAwait(false); } catch { }
                if (DateTime.UtcNow > enqueueDeadlineUtc) { deadlineHit = true; break; }
            }

            // 入队结束标记（若门限命中则立即标记结束）
            outQueue.Enqueue((null, null, true));
            try { await consumeTask.ConfigureAwait(false); } catch { }

            // 统一结束：根据是否完整完成来决定 Completed/Aborted
            var completedAll = (!aborted && actualRounds >= rounds);
            await TryEndSessionAsync(completedAll ? null : "Aborted", completedAll).ConfigureAwait(false);

            if (aborted || actualRounds < rounds)
            {
                var nm = "RimAI.Stage.GroupChat.NoContent".Translate().ToString();
                return new ActResult { Completed = false, Reason = aborted ? "Aborted" : "NoContent", FinalText = nm, Rounds = 0 };
            }
            finalTranscript = transcript.ToString().TrimEnd();
            return new ActResult { Completed = true, Reason = "Completed", FinalText = finalTranscript, Rounds = actualRounds };
        }

        public async Task<StageIntent> TryBuildAutoIntentAsync(CancellationToken ct)
        {
            try
            {
                var ids = await _worldData.GetAllColonistLoadIdsAsync(ct).ConfigureAwait(false);
                var list = ids?.ToList() ?? new List<int>();
                if (list.Count < 2) return null;
                var rnd = new Random(unchecked(Environment.TickCount ^ (list.Count << 3)));
                int centerIdx = rnd.Next(0, list.Count);
                int center = list[centerIdx];
                int count = Math.Max(2, Math.Min(5, 2 + rnd.Next(0, 4))); // 2..5
                int rounds = Math.Max(1, Math.Min(3, 1 + rnd.Next(0, 3))); // 1..3
                var pool = list.Where(x => x != center).OrderBy(_ => rnd.Next()).Take(Math.Max(1, count - 1)).ToList();
                pool.Insert(0, center);
                var participants = pool.Select(x => $"pawn:{x}").ToList();
                var scenario = $"groupchat:auto rounds={rounds}; participants={string.Join(",", participants)}";
                return new StageIntent
                {
                    ActName = Name,
                    ParticipantIds = participants,
                    Origin = "Global",
                    ScenarioText = scenario,
                    Locale = _loc?.GetDefaultLocale() ?? "en",
                    Seed = DateTime.UtcNow.Ticks.ToString()
                };
            }
            catch { return null; }
        }

        private static string BuildUserPromptSimpleMap(List<string> participants, int round)
        {
            // JSON 模板固定：{"1":"...","2":"...",...}，按参与者顺序映射 1..N；不要输出解释文本
            var template = BuildJsonTemplate(participants.Count);
            return $"现在，生成第{round}轮对话。请严格输出 JSON，键为 1..{participants.Count}，值为对应角色的台词（简短口语化），不得包含解释文本：{template}";
        }

        private static string BuildJsonTemplate(int count)
        {
            var mapKeys = new List<string>();
            for (int i = 0; i < count; i++) mapKeys.Add($"\"{i + 1}\":\"...\"");
            return "{" + string.Join(",", mapKeys) + "}";
        }
        
    }
}


