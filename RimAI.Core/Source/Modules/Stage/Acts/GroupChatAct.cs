using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
    internal sealed class GroupChatAct : IStageAct
    {
        private readonly ILLMService _llm;
        private readonly IWorldActionService _worldAction;
        public GroupChatAct(ILLMService llm, IWorldActionService worldAction = null) { _llm = llm; _worldAction = worldAction; }

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

            // 聚会：以第一个参与者为发起者（若可用）
            if (_worldAction != null)
            {
                try
                {
                    var first = participants.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(first) && first.StartsWith("pawn:"))
                    {
                        if (int.TryParse(first.Substring(5), out var id))
                        {
                            try { await _worldAction.TryStartPartyAsync(id, ct).ConfigureAwait(false); } catch { }
                        }
                    }
                }
                catch { }
            }

            int actualRounds = 0;
            for (int r = 1; r <= rounds && !ct.IsCancellationRequested; r++)
            {
                var system = "你是 RimWorld 殖民地中的对话导演。一次性返回本轮所有发言，严格 JSON，无解释。字段：round, utterances=[{speaker:'pawn:<loadId>', text:'...'}]. 句子简短、口语化，避免敏感内容。";
                var user = BuildUserPrompt(participants, r);
                var chatReq = new RimAI.Framework.Contracts.UnifiedChatRequest
                {
                    ConversationId = conv,
                    Messages = new List<RimAI.Framework.Contracts.ChatMessage>
                    {
                        new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=system },
                        new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=user }
                    },
                    Stream = false
                };
                var resp = await _llm.GetResponseAsync(chatReq, ct).ConfigureAwait(false);
                if (!resp.IsSuccess) break;
                var json = resp.Value?.Message?.Content ?? string.Empty;
                try
                {
                    var shaped = JsonConvert.DeserializeObject<GroupChatRound>(json);
                    if (shaped?.utterances != null)
                    {
                        actualRounds++;
                        if (_worldAction != null)
                        {
                            foreach (var u in shaped.utterances)
                            {
                                if (u == null || string.IsNullOrWhiteSpace(u.speaker) || string.IsNullOrWhiteSpace(u.text)) continue;
                                if (!u.speaker.StartsWith("pawn:")) continue;
                                if (int.TryParse(u.speaker.Substring(5), out var pid))
                                {
                                    try { await _worldAction.ShowSpeechTextAsync(pid, u.text, ct).ConfigureAwait(false); } catch { }
                                }
                            }
                        }
                    }
                }
                catch { break; }
            }

            if (actualRounds <= 0)
            {
                return new ActResult { Completed = false, Reason = "NoContent", FinalText = "（本次群聊无有效输出）", Rounds = 0 };
            }
            return new ActResult { Completed = true, Reason = "Completed", FinalText = "群聊完成", Rounds = actualRounds };
        }

        private static string BuildUserPrompt(List<string> participants, int round)
        {
            var speakers = string.Join(", ", participants);
            var scaffold = new
            {
                round = round,
                utterances = new[] { new { speaker = "pawn:<loadId>", text = "..." } }
            };
            var json = JsonConvert.SerializeObject(scaffold);
            return $"第{round}轮群聊。参与者：{speakers}。请直接输出 JSON：{json}";
        }
        
        private sealed class GroupChatRound
        {
            public int round { get; set; }
            public List<Utterance> utterances { get; set; }
        }
        private sealed class Utterance
        {
            public string speaker { get; set; }
            public string text { get; set; }
        }
    }
}


