using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
// using RimAI.Core.Infrastructure.Cache; // 缓存已下沉至 Framework
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Infrastructure.Extensions;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Modules.LLM
{
    internal sealed class LLMService : ILLMService
    {
        private readonly IConfigurationService _config;

        public int LastRetries { get; private set; }

        public LLMService(IConfigurationService config)
        {
            _config = config;
        }

        public async Task<string> GetResponseAsync(string prompt, bool forceJson = false, CancellationToken ct = default)
        {
            LastRetries = 0;

            var request = new UnifiedChatRequest
            {
                ForceJsonOutput = forceJson,
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = prompt }
                }
            };

            var result = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            if (!result.IsSuccess)
                throw new Exception($"LLM Error: {result.Error}");

            return result.Value.Message.Content;
        }

        public async Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, CancellationToken ct = default)
        {
            var res = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            return res;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // 流式过程中用 try/catch 将异常转换为失败块，避免 UI 中断
            IAsyncEnumerable<Result<UnifiedChatChunk>> stream = null;
            string initError = null;
            try
            {
                stream = RimAIApi.StreamCompletionAsync(request, ct);
            }
            catch (Exception ex)
            {
                initError = $"Stream init error: {ex.Message}";
            }
            if (initError != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(initError);
                yield break;
            }

            await foreach (var chunk in stream.WrapErrors(
                               ex => Result<UnifiedChatChunk>.Failure($"Stream error: {ex.Message}"),
                               ct))
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

        // 缓存键计算逻辑已移除，统一由 Framework 层处理缓存
    }
}