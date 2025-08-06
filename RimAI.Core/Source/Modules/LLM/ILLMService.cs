using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.LLM
{
    public interface ILLMService
    {
        /// <summary>
        /// 简单非流式请求，返回完整文本。
        /// </summary>
        Task<string> GetResponseAsync(string prompt, bool forceJson = false, CancellationToken ct = default);

        /// <summary>
        /// 统一请求模型的非流式响应，返回完整结构体。
        /// </summary>
        Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, CancellationToken ct = default);

        /// <summary>
        /// 流式响应。
        /// </summary>
        IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request, CancellationToken ct = default);

        /// <summary>
        /// 最近一次调用的重试次数。
        /// </summary>
        int LastRetries { get; }

        /// <summary>
        /// 最近一次调用是否命中缓存。
        /// </summary>
        bool LastFromCache { get; }

        /// <summary>
        /// 累计缓存命中次数。
        /// </summary>
        int CacheHits { get; }
    }
}