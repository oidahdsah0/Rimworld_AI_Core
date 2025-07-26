using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;  // 引入 Newtonsoft.Json，用于序列化和反序列化
using RimAI.Core.Contracts.Services;
using RimAI.Framework.API;
// 使用命名空间别名避免冲突
using RimAIFrame = RimAI.Framework.LLM.Models;

namespace RimAI.Core.Services
{
    public class LLMService : ILLMService // 3. 实现我们定义的接口
    {
        // --- 依赖注入 ---
        private readonly IConfigurationService _configService;
        private readonly ICacheService<string, LLMResponse> _cacheService;

        // --- 构造函数 ---
        // 启动时注入依赖的服务
        public LLMService(IConfigurationService configService, ICacheService<string, LLMResponse> cacheService)
        {
            _configService = configService;
            _cacheService = cacheService;
        }

        // --- 实现接口 ---
        public async Task<LLMResponse> SendMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null)
        {
            // 生成缓存键
            var cacheKey = GenerateCacheKey(messages, options);

            // 检查缓存
            if (_cacheService.TryGetValue(cacheKey, out var cachedResponse))
            {
                return cachedResponse;  // 如果缓存命中，直接返回缓存结果
            }

            // 缓存未命中，调用私有辅助方法执行实际的API调用
            var response = await ExecuteApiCallAsync(messages, options);

            // 如果API调用成功，将结果缓存
            if (response.IsSuccess)
            {
                var cacheDuration = TimeSpan.FromMinutes(_configService.Current.Cache.CacheDurationMinutes);
                _cacheService.Set(cacheKey, response, cacheDuration);
            }

            // 返回最终响应
            return response;
        }

        public IAsyncEnumerable<string> StreamMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<LLMToolCall>> GetToolCallsAsync(List<LLMChatMessage> messages, List<LLMToolFunction> availableTools, LLMRequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        // --- 私有辅助方法 (Private Helper Methods) ---

        // 生成缓存键的私有方法
        private string GenerateCacheKey(object requestPayload, LLMRequestOptions options)
        {
            // 将请求内容和选项序列化为JSON字符串
            var serializedPayload = JsonConvert.SerializeObject(requestPayload);
            var serializedOptions = options != null ? JsonConvert.SerializeObject(options) : string.Empty;

            // 使用SHA256计算哈希值
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(serializedPayload + serializedOptions);
                var hashBytes = sha256.ComputeHash(bytes);

                // 将哈希值转换为十六进制字符串
                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}