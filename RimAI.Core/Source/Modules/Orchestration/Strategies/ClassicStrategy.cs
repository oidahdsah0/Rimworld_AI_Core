using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure.Cache;
using RimAI.Core.Modules.LLM;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Orchestration.Strategies
{
    /// <summary>
    /// 迁移自 OrchestrationService 的“五步工作流”实现，语义保持一致。
    /// </summary>
    internal sealed class ClassicStrategy : IOrchestrationStrategy
    {
        public string Name => "Classic";

        private readonly ILLMService _llm;
        private readonly IToolRegistryService _tools;
        private readonly ICacheService _cache;
        private readonly IPersonaService _personaService;

        public ClassicStrategy(ILLMService llm, IToolRegistryService tools, ICacheService cache, IPersonaService personaService)
        {
            _llm = llm;
            _tools = tools;
            _cache = cache;
            _personaService = personaService;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context)
        {
            var query = context.Query ?? string.Empty;
            var personaSystemPrompt = context.PersonaSystemPrompt ?? string.Empty;

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
                    ["name"] = schema?.Name ?? string.Empty,
                    ["description"] = schema?.Description ?? string.Empty,
                    ["parameters"] = JObject.Parse(string.IsNullOrWhiteSpace(schema?.Arguments) ? "{}" : schema.Arguments)
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

            var call = decisionRes.Value?.Message?.ToolCalls?.FirstOrDefault();
            if (call == null || string.IsNullOrWhiteSpace(call.Function?.Name))
            {
                // 如果不需要工具调用，直接返回LLM的回答
                var direct = decisionRes.Value?.Message?.Content;
                if (!string.IsNullOrEmpty(direct))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = direct });
                    yield break;
                }

                yield return Result<UnifiedChatChunk>.Failure("LLM 未返回有效的 tool_calls 或直接回答。");
                yield break;
            }

            var argsDict = new Dictionary<string, object>();
            string parseArgsError = null;
            try
            {
                var argsStr = call.Function?.Arguments;
                if (!string.IsNullOrWhiteSpace(argsStr) && argsStr != "{}")
                {
                    var jObj = JObject.Parse(argsStr);
                    argsDict = jObj.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }
            }
            catch (System.Exception ex)
            {
                parseArgsError = $"解析 tool 参数失败: {ex.Message}";
            }
            if (parseArgsError != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(parseArgsError);
                yield break;
            }

            object toolResult = null;
            IAsyncEnumerable<Result<UnifiedChatChunk>> errorStream = null;

            try
            {
                toolResult = await _tools.ExecuteToolAsync(call.Function.Name, argsDict);
            }
            catch (System.Exception ex)
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
            var cacheKey = ComputeSummarizationCacheKey(query, toolResultJson ?? string.Empty);

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
                _cache.Set(cacheKey, finalResponse, System.TimeSpan.FromMinutes(5));
            }
        }

        private static List<ChatMessage> BuildBaseMessages(string personaPrompt, string userQuery)
        {
            var msgs = new List<ChatMessage>();
            if (!string.IsNullOrWhiteSpace(personaPrompt))
            {
                msgs.Add(new ChatMessage { Role = "system", Content = personaPrompt });
            }
            msgs.Add(new ChatMessage { Role = "user", Content = userQuery ?? string.Empty });
            return msgs;
        }

        private static string ComputeSummarizationCacheKey(string query, string toolResultJson)
        {
            var combined = $"{query}|{toolResultJson}";
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}


