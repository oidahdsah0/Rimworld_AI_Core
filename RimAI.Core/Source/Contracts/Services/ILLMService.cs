using System.Collections.Generic;
using System.Threading.Tasks;

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
        /// <summary>
        /// 发送消息并获取一个完整的、非流式的回复。
        /// </summary>
        Task<LLMResponse> SendMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null);

        /// <summary>
        /// 发送消息并以异步流的方式逐块获取回复，用于实时显示。
        /// </summary>
        IAsyncEnumerable<string> StreamMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null);

        /// <summary>
        /// 发送包含可用工具列表的消息，并获取AI决定要调用的工具列表。
        /// </summary>
        Task<List<LLMToolCall>> GetToolCallsAsync(List<LLMChatMessage> messages, List<LLMToolFunction> availableTools, LLMRequestOptions options = null);
    }
}