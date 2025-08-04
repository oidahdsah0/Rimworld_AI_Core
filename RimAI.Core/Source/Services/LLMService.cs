using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;
using RimAI.Framework.Shared.Exceptions;

namespace RimAI.Core.Services
{
    public class LLMService : ILLMService
    {
        private readonly IConfigurationService _configService;
        private readonly ICacheService<string, UnifiedChatResponse> _cacheService;
        private readonly RimAI.Core.Contracts.Data.ResilienceConfig _resilience;

        public LLMService(IConfigurationService configService, ICacheService<string, UnifiedChatResponse> cacheService)
        {
            _configService = configService;
            _cacheService = cacheService;
            _resilience = _configService.Current.LLM.Resilience;
        }

        public async Task<Result<UnifiedChatResponse>> SendChatAsync(UnifiedChatRequest request)
        {
            var cacheKey = RimAI.Core.Architecture.Caching.CacheKeyUtil.GenerateChatRequestKey(request);
            if (_cacheService.TryGetValue(cacheKey, out var cached))
            {
                RimAI.Framework.Shared.Logging.RimAILogger.Log("[LLM] Cache hit " + cacheKey);
                return Result<UnifiedChatResponse>.Success(cached);
            }

            // Circuit breaker：若熔断打开则立即失败
            if (IsCircuitOpen())
            {
                var ex = new LLMException("Circuit breaker is open; request blocked.");
                return Result<UnifiedChatResponse>.Failure(ex.Message);
            }

            var retries = _resilience.MaxRetries;
            var delay = 1000; // 初始 1 秒
            for (var attempt = 0; attempt <= retries; attempt++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var result = await RimAIApi.GetCompletionAsync(request);
                sw.Stop();

                if (result.IsSuccess)
                {
                    ResetFailureWindow();
                    var duration = TimeSpan.FromMinutes(_configService.Current.Cache.CacheDurationMinutes);
                    _cacheService.Set(cacheKey, result.Value, duration);
                    RimAI.Framework.Shared.Logging.RimAILogger.Log($"[LLM] Success in {sw.ElapsedMilliseconds}ms, attempt {attempt + 1}");
                    return result;
                }

                RegisterFailure();
                RimAI.Framework.Shared.Logging.RimAILogger.Warning($"[LLM] Failure attempt {attempt + 1}: {result.Error}");

                if (attempt == retries)
                {
                    // 最终失败
                    return result;
                }

                await Task.Delay(delay);
                delay *= 2; // 指数退避
            }
            // 理论不会到这里
            return Result<UnifiedChatResponse>.Failure("Unknown failure");
        }

        #region CircuitBreaker Helpers
        private int _failureCount;
        private DateTime _windowStart = DateTime.UtcNow;
        private DateTime _circuitOpenUntil;

        private void RegisterFailure()
        {
            var now = DateTime.UtcNow;
            if ((now - _windowStart).TotalSeconds > _resilience.CircuitBreakerWindowSeconds)
            {
                // 重新开始新窗口
                _windowStart = now;
                _failureCount = 0;
            }
            _failureCount++;
            if (_failureCount >= _resilience.CircuitBreakerFailureThreshold)
            {
                _circuitOpenUntil = now.AddSeconds(_resilience.CircuitBreakerCooldownSeconds);
                RimAI.Framework.Shared.Logging.RimAILogger.Warning("[LLM] Circuit opened for " + _resilience.CircuitBreakerCooldownSeconds + "s");
            }
        }

        private bool IsCircuitOpen()
        {
            if (_circuitOpenUntil == default) return false;
            if (DateTime.UtcNow >= _circuitOpenUntil)
            {
                // 进入半开状态，重置
                _circuitOpenUntil = default;
                _failureCount = 0;
                return false;
            }
            return true;
        }

        private void ResetFailureWindow()
        {
            _failureCount = 0;
            _windowStart = DateTime.UtcNow;
        }
        #endregion

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request)
        {
            var cacheKey = RimAI.Core.Architecture.Caching.CacheKeyUtil.GenerateChatRequestKey(request);

            var builder = new System.Text.StringBuilder();
            // 透传底层流，同时收集内容
            await foreach (var result in RimAIApi.StreamCompletionAsync(request))
            {
                if (result.IsSuccess && result.Value != null)
                {
                    var chunk = result.Value;
                    if (!string.IsNullOrEmpty(chunk.ContentDelta))
                    {
                        builder.Append(chunk.ContentDelta);
                    }
                }

                // 向调用方立即转发
                yield return result;

                // 当检测到流结束时（FinishReason 不为空）
                if (result.IsSuccess && result.Value != null && !string.IsNullOrEmpty(result.Value.FinishReason))
                {
                    // 仅当成功且未被工具调用中断时缓存
                    var response = new UnifiedChatResponse
                    {
                        FinishReason = result.Value.FinishReason,
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = builder.ToString()
                        }
                    };

                    var duration = TimeSpan.FromMinutes(_configService.Current.Cache.CacheDurationMinutes);
                    _cacheService.Set(cacheKey, response, duration);
                }
            }
        }

        public Task<Result<UnifiedChatResponse>> SendChatWithToolsAsync(List<ChatMessage> messages, List<ToolDefinition> tools)
        {
            return RimAIApi.GetCompletionWithToolsAsync(messages, tools);
        }
    }
}