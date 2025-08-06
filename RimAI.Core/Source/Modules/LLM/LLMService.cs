using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Infrastructure.Cache;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Modules.LLM
{
    internal sealed class LLMService : ILLMService
    {
        private readonly IConfigurationService _config;
        private readonly ICacheService _cache;

        public int LastRetries { get; private set; }
        public bool LastFromCache { get; private set; }
        public int CacheHits => _cache.HitCount;

        public LLMService(IConfigurationService config, ICacheService cache)
        {
            _config = config;
            _cache = cache;
        }

        public async Task<string> GetResponseAsync(string prompt, bool forceJson = false, CancellationToken ct = default)
        {
            LastRetries = 0;
            LastFromCache = false;

            var request = new UnifiedChatRequest
            {
                ForceJsonOutput = forceJson,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = prompt }
                }
            };

            var cacheKey = ComputeCacheKey(request);
            if (_cache.TryGet(cacheKey, out string cached))
            {
                LastFromCache = true;
                return cached;
            }

            var result = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            if (!result.IsSuccess)
                throw new Exception($"LLM Error: {result.Error}");

            var content = result.Value.Message.Content;
            _cache.Set(cacheKey, content, TimeSpan.FromMinutes(_config.Current.Cache.DefaultExpirationMinutes));
            return content;
        }

        public async Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, CancellationToken ct = default)
        {
            // 非流式请求，使用缓存逻辑
            var cacheKey = ComputeCacheKey(request);
            if (_cache.TryGet(cacheKey, out string cachedJson))
            {
                LastFromCache = true;
                var cachedResponse = new UnifiedChatResponse
                {
                    FinishReason = "stop",
                    Message = new ChatMessage { Role = "assistant", Content = cachedJson }
                };
                return Result<UnifiedChatResponse>.Success(cachedResponse);
            }

            var res = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            if (res.IsSuccess)
            {
                var content = res.Value.Message.Content;
                _cache.Set(cacheKey, content, TimeSpan.FromMinutes(_config.Current.Cache.DefaultExpirationMinutes));
            }
            return res;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await foreach (var chunk in RimAIApi.StreamCompletionAsync(request, ct))
            {
                yield return chunk;
            }
        }

        private async Task<Result<UnifiedChatResponse>> ExecuteWithRetryAsync(Func<Task<Result<UnifiedChatResponse>>> action)
        {
            var maxRetries = 3;
            var delay = 1000;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var res = await action();
                if (res.IsSuccess)
                {
                    LastRetries = attempt;
                    return res;
                }

                CoreServices.Logger.Warn($"[Retry #{attempt + 1}] {res.Error}");
                await Task.Delay(delay);
                delay *= 2;
            }

            // 最后一次执行
            var finalRes = await action();
            LastRetries = maxRetries;
            return finalRes;
        }

        private static string ComputeCacheKey(UnifiedChatRequest req)
        {
            var json = JsonConvert.SerializeObject(req);
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}