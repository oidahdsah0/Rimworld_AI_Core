using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
    internal sealed class GroupChatAct : IStageAct
    {
        private readonly ILLMService _llm;
        public GroupChatAct(ILLMService llm) { _llm = llm; }

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
            var system = "你是 RimWorld 殖民地中的友好助手，进行简短的群聊回复。";
            var user = string.IsNullOrWhiteSpace(req?.ScenarioText) ? "开始一次简短的寒暄。" : req.ScenarioText;

            var chatReq = new RimAI.Framework.Contracts.UnifiedChatRequest
            {
                ConversationId = conv,
                Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
                {
                    new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content=system },
                    new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content=user }
                },
                Stream = false
            };
            var resp = await _llm.GetResponseAsync(chatReq, ct).ConfigureAwait(false);
            if (!resp.IsSuccess)
            {
                return new ActResult { Completed = false, Reason = resp.Error ?? "Error", FinalText = "（群聊失败或超时）" };
            }
            var text = resp.Value?.Message?.Content ?? string.Empty;
            return new ActResult { Completed = true, Reason = "Completed", FinalText = text, Rounds = 1 };
        }
    }
}


