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
            // 为单轮便捷接口自动设置会话ID（以内容哈希稳定标识，避免API拒绝）
            request.ConversationId = $"adhoc:single:{ComputeShortHash(prompt ?? string.Empty)}";

            var result = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            if (!result.IsSuccess)
                throw new Exception($"LLM Error: {result.Error}");

            return result.Value.Message.Content;
        }

        public async Task<Result<UnifiedChatResponse>> GetResponseAsync(UnifiedChatRequest request, CancellationToken ct = default)
        {
            EnsureConversationId(request, "adhoc:unified");
            var res = await ExecuteWithRetryAsync(() => RimAIApi.GetCompletionAsync(request, ct));
            return res;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            EnsureConversationId(request, "adhoc:stream");
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

        private static string ComputeShortHash(string input)
        {
            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
                    var hash = sha1.ComputeHash(bytes);
                    // 取前 10 字节（20个hex字符）作为短哈希，兼顾稳定与长度
                    var sb = new System.Text.StringBuilder(20);
                    for (int i = 0; i < Math.Min(hash.Length, 10); i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "0000000000"; }
        }

        private static void EnsureConversationId(UnifiedChatRequest request, string prefix)
        {
            if (request == null) return;
            if (!string.IsNullOrWhiteSpace(request.ConversationId)) return;
            string basis = string.Empty;
            try
            {
                if (request.Messages != null && request.Messages.Count > 0)
                {
                    // 取首条与末条消息内容做为基础，避免过长
                    var first = request.Messages[0]?.Content ?? string.Empty;
                    var last = request.Messages[request.Messages.Count - 1]?.Content ?? string.Empty;
                    basis = first + "\n" + last;
                }
            }
            catch { basis = string.Empty; }
            request.ConversationId = $"{prefix}:{ComputeShortHash(basis)}";
        }
    }
}