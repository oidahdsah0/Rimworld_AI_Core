using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.API;            // Result<T>
using RimAI.Framework.Contracts;   // UnifiedChatRequest, ChatMessage, UnifiedChatChunk, ToolDefinition

namespace RimAI.Core.Contracts.Services
{
    // --- 数据模型 (Data Models) ---

    /// <summary>
    /// 代表一次对话中的单条消息。是我们与LLM沟通的基础单元。
    /// </summary>
    public class LLMChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    /// <summary>
    /// 定义LLM请求的可选参数，用于微调AI的行为。
    /// </summary>
    public class LLMRequestOptions
    {
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public List<string> StopSequences { get; set; } = new List<string>();
    }

    /// <summary>
    /// 封装从LLM服务返回的所有信息。这是我们Core模块内部流通的“标准商品”。
    /// </summary>
    public class LLMResponse
    {
        public string Id { get; set; }
        public string Model { get; set; }
        public string Content { get; set; }
        public List<ToolCall> ToolCalls { get; set; }
        public string ErrorMessage { get; set; }
        public LLMUsageInfo Usage { get; set; }
    }

    /// <summary>
    /// 记录该次API调用的Token使用情况。
    /// </summary>
    public class LLMUsageInfo
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    /// <summary>
    /// 向LLM描述一个我们可以使用的工具（函数）。
    /// </summary>
    public class LLMToolFunction
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public object Parameters { get; set; }
    }

    /// <summary>
    /// 代表LLM决定要执行的一次具体的工具调用。
    /// </summary>
    public class LLMToolCall
    {
        public string Id { get; set; }  // Tool call的唯一ID
        public string Name { get; set; }    // 要调用的函数名
        public Dictionary<string, object> Arguments { get; set; }   // 参数被反序列化为字典，方便内部使用
    }

    // --- 服务接口 (Service Interface) ---

    /// <summary>
    /// 定义了Core模块与所有大语言模型交互的统一网关。
    /// </summary>
    public interface ILLMService
    {
        // ===== 旧接口（将于 v3.2 移除） =====

        /// <summary>
        /// 【已弃用】发送消息并获取完整回复。请迁移至 <see cref="SendChatAsync"/>。
        /// </summary>
        [System.Obsolete("Use SendChatAsync instead. Will be removed in v3.2.")]
        Task<LLMResponse> SendMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null);

        /// <summary>
        /// 【已弃用】流式回复接口。请迁移至 <see cref="StreamResponseAsync"/>。
        /// </summary>
        [System.Obsolete("Use StreamResponseAsync instead. Will be removed in v3.2.")]
        IAsyncEnumerable<string> StreamMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null);

        /// <summary>
        /// 【已弃用】工具调用辅助接口。请迁移至 SendChatWithToolsAsync。
        /// </summary>
        [System.Obsolete("Use SendChatWithToolsAsync instead. Will be removed in v3.2.")]
        Task<List<LLMToolCall>> GetToolCallsAsync(List<LLMChatMessage> messages, List<LLMToolFunction> availableTools, LLMRequestOptions options = null);

        // ===== 新接口（Framework v4.1 适配） =====

        /// <summary>
        /// 发送一次统一聊天请求，并获取完整结果（非流式）。
        /// 返回 Result，需检查 IsSuccess。失败时请映射至相应异常或错误处理逻辑。
        /// </summary>
        Task<Result<UnifiedChatResponse>> SendChatAsync(UnifiedChatRequest request);

        /// <summary>
        /// 发送聊天请求并以异步流方式获取增量结果。
        /// 每个流块都是 Result，需要在消费端检查 IsSuccess。
        /// </summary>
        IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request);

        /// <summary>
        /// 支持工具调用的场景，等价于 RimAIApi.GetCompletionWithToolsAsync 的封装。
        /// 当模型返回 FinishReason = "tool_calls" 时，调用层应继续工具工作流。
        /// </summary>
        Task<Result<UnifiedChatResponse>> SendChatWithToolsAsync(List<ChatMessage> messages, List<ToolDefinition> tools);
    }
}