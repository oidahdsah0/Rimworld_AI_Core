using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts;
using RimAI.Core.Modules.Orchestration.Modes;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 工具仅编排统一入口：不触达 LLM，不做自动判断或降级。
    /// </summary>
    internal sealed class OrchestrationService : IOrchestrationService
    {
        private readonly ToolMatchModeResolver _resolver;

        public OrchestrationService(ToolMatchModeResolver resolver)
        {
            _resolver = resolver;
        }

        public Task<ToolCallsResult> ExecuteAsync(
            string userInput,
            IReadOnlyList<string> participantIds,
            string mode,
            ToolOrchestrationOptions options = null,
            CancellationToken ct = default)
        {
            options ??= new ToolOrchestrationOptions { PreferredMode = mode };
            var impl = _resolver.Get(mode);
            return impl.ExecuteAsync(userInput ?? string.Empty, participantIds ?? new List<string>(), options, ct);
        }
    }
}
