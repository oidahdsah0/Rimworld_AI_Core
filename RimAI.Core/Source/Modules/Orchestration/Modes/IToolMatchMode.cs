using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts;

namespace RimAI.Core.Modules.Orchestration.Modes
{
    /// <summary>
    /// 工具匹配模式标准接口（工具仅编排，不触达 LLM）。
    /// </summary>
    internal interface IToolMatchMode
    {
        /// <summary>
        /// 模式名称（Classic/FastTop1/NarrowTopK/LightningFast）。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 执行一次工具链（该模式下不做跨模式降级）。
        /// </summary>
        Task<ToolCallsResult> ExecuteAsync(
            string userInput,
            IReadOnlyList<string> participantIds,
            ToolOrchestrationOptions options,
            CancellationToken ct);
    }
}


