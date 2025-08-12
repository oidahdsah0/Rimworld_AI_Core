using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Contracts
{
    /// <summary>
    /// 编排服务统一入口（工具仅编排）。
    /// 不触达 LLM，不做自动判断或降级，仅按显式模式执行工具链并返回结构化结果。
    /// </summary>
    public interface IOrchestrationService
    {
        /// <summary>
        /// 执行一次工具链（显式模式）。
        /// </summary>
        /// <param name="userInput">自然语言输入</param>
        /// <param name="participantIds">参与者标识列表（用于工具上下文）</param>
        /// <param name="mode">匹配模式：Classic/FastTop1/NarrowTopK/LightningFast（不支持 Auto）</param>
        /// <param name="options">可选参数（TopK 等）</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>结构化工具结果 <see cref="ToolCallsResult"/></returns>
        Task<ToolCallsResult> ExecuteAsync(
            string userInput,
            IReadOnlyList<string> participantIds,
            string mode,
            ToolOrchestrationOptions options = null,
            CancellationToken ct = default);
    }
}