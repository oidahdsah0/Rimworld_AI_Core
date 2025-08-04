using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;  // 引入 Newtonsoft.Json，用于序列化和反序列化
using RimAI.Core.Contracts.Services;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;


namespace RimAI.Core.Services
{
    public class LLMService : ILLMService // 3. 实现我们定义的接口
    {
        // --- 依赖注入 ---
        private readonly IConfigurationService _configService;
        private readonly ICacheService<string, UnifiedChatResponse> _cacheService;

        // --- 构造函数 ---
        // 启动时注入依赖的服务
        public LLMService(IConfigurationService configService, ICacheService<string, UnifiedChatResponse> cacheService)
        {
            _configService = configService;
            _cacheService = cacheService;
        }

        // --- 实现接口 ---
        // ===== 旧接口代理实现 =====
        public async Task<LLMResponse> SendMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null)
        {
            // 将旧消息模型简单映射为 Framework 的 ChatMessage
            var chatMessages = messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }).ToList();
            var request = new UnifiedChatRequest { Messages = chatMessages };
            var result = await SendChatAsync(request);
            if (result.IsSuccess)
            {
                return new LLMResponse
                {
                    Content = result.Value.Message.Content,
                    Model = "", // 可根据需要填充
                    Usage = null,
                    ToolCalls = result.Value.Message.ToolCalls
                };
            }
            return new LLMResponse { ErrorMessage = result.Error };
        }

        public async IAsyncEnumerable<string> StreamMessageAsync(List<LLMChatMessage> messages, LLMRequestOptions options = null)
        {
            var chatMessages = messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }).ToList();
            var request = new UnifiedChatRequest { Messages = chatMessages };

            await foreach (var chunk in StreamResponseAsync(request))
            {
                if (chunk.IsSuccess && chunk.Value.ContentDelta != null)
                {
                    yield return chunk.Value.ContentDelta;
                }
            }
        }

        public async Task<List<LLMToolCall>> GetToolCallsAsync(List<LLMChatMessage> messages, List<LLMToolFunction> availableTools, LLMRequestOptions options = null)
        {
            var chatMessages = messages.Select(m => new ChatMessage { Role = m.Role, Content = m.Content }).ToList();
            var tools = availableTools.Select(t => new ToolDefinition
            {
                Function = Newtonsoft.Json.Linq.JObject.FromObject(new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                })
            }).ToList();

            var result = await SendChatWithToolsAsync(chatMessages, tools);
            if (result.IsSuccess && result.Value.FinishReason == "tool_calls")
            {
                return result.Value.Message.ToolCalls.Select(tc => new LLMToolCall
                {
                    Id = tc.Id,
                    Name = tc.FunctionName,
                    Arguments = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(tc.Arguments)
                }).ToList();
            }
            return new List<LLMToolCall>();
        }

        // ===== 新接口实现 =====
        public async Task<Result<UnifiedChatResponse>> SendChatAsync(UnifiedChatRequest request)
        {
            var cacheKey = RimAI.Core.Architecture.Caching.CacheKeyUtil.GenerateChatRequestKey(request);
            if (_cacheService.TryGetValue(cacheKey, out var cached))
            {
                return Result<UnifiedChatResponse>.Success(cached);
            }

            var result = await RimAIApi.GetCompletionAsync(request);
            if (result.IsSuccess)
            {
                var duration = TimeSpan.FromMinutes(_configService.Current.Cache.CacheDurationMinutes);
                _cacheService.Set(cacheKey, result.Value, duration);
            }
            return result;
        }

        public IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(UnifiedChatRequest request)
        {
            // 直接返回底层异步流，不在这里缓存增量
            return RimAIApi.StreamCompletionAsync(request);
        }

        public Task<Result<UnifiedChatResponse>> SendChatWithToolsAsync(List<ChatMessage> messages, List<ToolDefinition> tools)
        {
            return RimAIApi.GetCompletionWithToolsAsync(messages, tools);
        }

        // --- 私有辅助方法 (Private Helper Methods) ---

        // 生成缓存键的私有方法
        
    }
}