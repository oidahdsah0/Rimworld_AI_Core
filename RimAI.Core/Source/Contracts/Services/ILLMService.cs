using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// Core 模块与 LLM Framework v4 交互的唯一网关接口。
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// 发送非流式聊天请求，并一次性获取完整结果。
        /// </summary>
        Task<Result<UnifiedChatResponse>> SendChatAsync(UnifiedChatRequest request);

        /// <summary>
        /// 发送聊天请求并以异步流形式获取增量结果。
        /// </summary>
        IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request);

        /// <summary>
        /// 含工具调用的聊天请求封装。当模型返回 FinishReason = "tool_calls" 时应继续工具工作流。
        /// </summary>
        Task<Result<UnifiedChatResponse>> SendChatWithToolsAsync(List<ChatMessage> messages, List<ToolDefinition> tools);
    }
}