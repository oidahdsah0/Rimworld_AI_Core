using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Modules.LLM;
using RimAI.Core.Infrastructure;
using System.Security.Cryptography;
using RimAI.Core.Infrastructure.Cache;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// P5 阶段 OrchestrationService 完整五步最小实现。
    /// </summary>
    internal sealed class OrchestrationService : IOrchestrationService
    {
        private readonly ILLMService _llm;
        private readonly IToolRegistryService _tools;
        private readonly ICacheService _cache;
        private readonly IPersonaService _personaService;

        public OrchestrationService(ILLMService llm, IToolRegistryService tools, ICacheService cache, IPersonaService personaService)
        {
            _llm = llm;
            _tools = tools;
            _cache = cache;
            _personaService = personaService;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string query, string personaSystemPrompt = "")
        {
            // 如果调用方未显式传入系统提示词，则使用当前 PersonaService 中的默认人格。
            if (string.IsNullOrWhiteSpace(personaSystemPrompt))
            {
                var def = _personaService.Get("Default");
                if (def != null)
                    personaSystemPrompt = def.SystemPrompt;
            }

            // Step 0: 构造 tools definition 列表供 LLM 决策
            var toolDefinitions = _tools.GetAllToolSchemas().Select(schema => new ToolDefinition
            {
                Type = "function",
                Function = new JObject
                {
                    ["name"] = schema.Name,
                    ["description"] = schema.Description,
                    ["parameters"] = JObject.Parse(string.IsNullOrWhiteSpace(schema.Arguments) ? "{}" : schema.Arguments)
                }
            }).ToList();

            // Step 1: 发送用户问题 + tools 给 LLM 决策
            var initMessages = BuildBaseMessages(personaSystemPrompt, query);
            var initReq = new UnifiedChatRequest
            {
                Stream = false,
                Tools  = toolDefinitions,
                Messages = initMessages
            };

            var decisionRes = await _llm.GetResponseAsync(initReq);
            if (!decisionRes.IsSuccess)
            {
                yield return Result<UnifiedChatChunk>.Failure(decisionRes.Error);
                yield break;
            }

            var call = decisionRes.Value.Message.ToolCalls?.FirstOrDefault();
            if (call == null || string.IsNullOrWhiteSpace(call.Function?.Name))
            {
                 // 如果不需要工具调用，直接返回LLM的回答
                if (!string.IsNullOrEmpty(decisionRes.Value.Message.Content))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = decisionRes.Value.Message.Content });
                    yield break;
                }

                yield return Result<UnifiedChatChunk>.Failure("LLM 未返回有效的 tool_calls 或直接回答。");
                yield break;
            }

            Dictionary<string, object> argsDict = new();
            string parseError = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(call.Function.Arguments) && call.Function.Arguments != "{}")
                {
                    var jObj = JObject.Parse(call.Function.Arguments);
                    argsDict = jObj.ToObject<Dictionary<string, object>>();
                }
            }
            catch (Exception ex)
            {
                parseError = $"解析 tool 参数失败: {ex.Message}";
            }

            if (parseError != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(parseError);
                yield break;
            }

            object toolResult = null;
            IAsyncEnumerable<Result<UnifiedChatChunk>> errorStream = null;
            
            try
            {
                toolResult = await _tools.ExecuteToolAsync(call.Function.Name, argsDict);
            }
            catch (Exception ex)
            {
                var errMessages = BuildBaseMessages(personaSystemPrompt, query);
                errMessages.Add(new ChatMessage { Role = "assistant", Content = $"调用工具 {call.Function.Name} 失败: {ex.Message}" });
                var errReq = new UnifiedChatRequest { Stream = true, Messages = errMessages };
                errorStream = _llm.StreamResponseAsync(errReq);
            }

            if (errorStream != null)
            {
                await foreach (var chunk in errorStream)
                    yield return chunk;
                yield break;
            }
            
            // --- 总结阶段缓存逻辑 ---
            var toolResultJson = JsonConvert.SerializeObject(toolResult, Formatting.None);
            var cacheKey = ComputeSummarizationCacheKey(query, toolResultJson);

            if (_cache.TryGet(cacheKey, out string cachedSummary))
            {
                yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = cachedSummary });
                yield break;
            }
            
            // --- 未命中缓存，正常发起流式请求 ---
            var followMessages = BuildBaseMessages(personaSystemPrompt, query);
            followMessages.Add(new ChatMessage { Role = "assistant", ToolCalls = new List<ToolCall> { call } });
            followMessages.Add(new ChatMessage { Role = "tool", ToolCallId = call.Id, Content = toolResultJson });

            var followReq = new UnifiedChatRequest
            {
                Stream = true,
                Tools = toolDefinitions,
                Messages = followMessages
            };

            var finalResponseBuilder = new StringBuilder();
            await foreach (var chunk in _llm.StreamResponseAsync(followReq))
            {
                if (chunk.IsSuccess && !string.IsNullOrEmpty(chunk.Value?.ContentDelta))
                {
                    finalResponseBuilder.Append(chunk.Value.ContentDelta);
                }
                yield return chunk;
            }

            // --- 将最终结果写入缓存 ---
            var finalResponse = finalResponseBuilder.ToString();
            if (!string.IsNullOrEmpty(finalResponse))
            {
                _cache.Set(cacheKey, finalResponse, TimeSpan.FromMinutes(5));
            }
        }
        
        private string ComputeSummarizationCacheKey(string query, string toolResultJson)
        {
            var combined = $"{query}|{toolResultJson}";
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        private static List<ChatMessage> BuildBaseMessages(string personaPrompt, string userQuery)
        {
            var msgs = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(personaPrompt))
            {
                msgs.Add(new ChatMessage { Role = "system", Content = personaPrompt });
            }
            msgs.Add(new ChatMessage { Role = "user", Content = userQuery });
            return msgs;
        }
    }
}
