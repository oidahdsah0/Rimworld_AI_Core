using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Acts
{
    internal sealed class AlphaFiberInterServerChatAct : IStageAct
    {
        private readonly ILLMService _llm;
        private readonly RimAI.Core.Source.Modules.World.IWorldDataService _world;
        public AlphaFiberInterServerChatAct(ILLMService llm, RimAI.Core.Source.Modules.World.IWorldDataService world)
        {
            _llm = llm; _world = world;
        }

        public string Name => "AlphaFiberInterServerChat";

        public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
        public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

        public bool IsEligible(StageExecutionRequest req)
        {
            return req?.Ticket != null;
        }

        public async Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct)
        {
            var conv = req?.Ticket?.ConvKey ?? ("agent:stage|" + (DateTime.UtcNow.Ticks));
            var system = "你是负责跨服务器光纤链路通讯的助手，回答要简洁明了。";
            var user = string.IsNullOrWhiteSpace(req?.ScenarioText) ? "与邻近服务器建立一次状态同步的简短问候。" : req.ScenarioText;

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
                return new ActResult { Completed = false, Reason = resp.Error ?? "Error", FinalText = "（链路对话失败或超时）" };
            }
            var text = resp.Value?.Message?.Content ?? string.Empty;
            return new ActResult { Completed = true, Reason = "Completed", FinalText = text, Rounds = 1 };
        }
    }
}


