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
    }
}


