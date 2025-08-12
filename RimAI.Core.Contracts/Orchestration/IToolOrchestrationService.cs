using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Contracts
{
    /// <summary>
    /// 工具仅编排服务：不触达 LLM，仅负责工具匹配/执行/聚合并返回结构化结果。
    /// </summary>
    public interface IToolOrchestrationService
    {
        Task<ToolCallsResult> ExecuteAsync(
            string userInput,
            IReadOnlyList<string> participantIds,
            ToolOrchestrationOptions options = null,
            CancellationToken ct = default);
    }

    /// <summary>
    /// 工具编排可选项。
    /// </summary>
    public sealed class ToolOrchestrationOptions
    {
        /// <summary>
        /// 匹配模式：Classic/NarrowTopK/FastTop1/LightningFast（不支持 Auto）。
        /// </summary>
        public string PreferredMode { get; set; }

        /// <summary>
        /// 当使用 NarrowTopK 时的 TopK（可覆盖配置）。
        /// </summary>
        public int? TopK { get; set; }
    }

    /// <summary>
    /// 单次工具调用记录。
    /// </summary>
    public sealed class ToolCallRecord
    {
        public string ToolName { get; set; }
        public string ArgumentsJson { get; set; }
        public bool Succeeded { get; set; }
        public string ResultType { get; set; }
        public string ResultJson { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// 工具仅编排的结构化结果。
    /// </summary>
    public sealed class ToolCallsResult
    {
        public string SelectedMode { get; set; }
        public string SelectedToolName { get; set; }
        public bool UsedLightningFast { get; set; }

        public List<ToolCallRecord> Calls { get; set; } = new List<ToolCallRecord>();

        public string Notes { get; set; }
        public int DurationMs { get; set; }
    }
}


