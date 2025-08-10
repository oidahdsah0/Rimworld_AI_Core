using System.Collections.Generic;
using System.Threading;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Orchestration.Strategies
{
    internal interface IOrchestrationStrategy
    {
        string Name { get; }

        IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context);
    }

    internal sealed class OrchestrationContext
    {
        public string Query { get; init; }
        public string PersonaSystemPrompt { get; init; }
        public CancellationToken Cancellation { get; init; }
        // 未来可考虑把参与者集合传入，用于更稳定的 ConversationId；当前策略层按 query+persona 衍生
    }
}


