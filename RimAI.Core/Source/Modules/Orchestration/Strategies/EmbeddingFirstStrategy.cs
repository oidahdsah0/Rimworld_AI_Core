using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Modules.Embedding;
using RimAI.Core.Modules.LLM;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Orchestration.Strategies
{
    internal sealed class EmbeddingFirstStrategy : IOrchestrationStrategy
    {
        public string Name => "EmbeddingFirst";
        private readonly IEmbeddingService _embedding;
        private readonly IRagIndexService _rag;
        private readonly ILLMService _llm;
        private readonly IToolRegistryService _tools;

        public EmbeddingFirstStrategy(IEmbeddingService embedding, IRagIndexService rag, ILLMService llm, IToolRegistryService tools)
        {
            _embedding = embedding;
            _rag = rag;
            _llm = llm;
            _tools = tools;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context)
        {
            var query = context.Query ?? string.Empty;
            var persona = context.PersonaSystemPrompt ?? string.Empty;

            // Step 0: RAG 预处理
            float[] qv = null;
            string failure = null;
            try { qv = await _embedding.GetEmbeddingAsync(query); }
            catch (System.Exception ex) { failure = $"Embedding 失败: {ex.Message}"; }
            if (failure != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(failure);
                yield break;
            }

            var hits = await _rag.QueryAsync(qv, topK: 5);
            if (hits != null && hits.Count > 0)
            {
                var preview = string.Join(", ", hits.Take(5).Select(h => $"{h.DocId}:{h.Score:F2}"));
                RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[EmbeddingFirst] RAG hits: {preview}");
            }
            var injectedContext = BuildInjectedContext(hits);

            // 构造 tools 定义
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

            // 将 RAG 上下文注入 system 提示
            var systemPrompt = CombineSystemPrompt(persona, injectedContext);
            var initMessages = new List<ChatMessage> { new ChatMessage { Role = "system", Content = systemPrompt }, new ChatMessage { Role = "user", Content = query } };
            var initReq = new UnifiedChatRequest { Stream = false, Tools = toolDefinitions, Messages = initMessages };

            var decisionRes = await _llm.GetResponseAsync(initReq);
            if (!decisionRes.IsSuccess)
            {
                yield return Result<UnifiedChatChunk>.Failure(decisionRes.Error);
                yield break;
            }

            var call = decisionRes.Value?.Message?.ToolCalls?.FirstOrDefault();
            if (call == null || string.IsNullOrWhiteSpace(call.Function?.Name))
            {
                var direct = decisionRes.Value?.Message?.Content;
                if (!string.IsNullOrEmpty(direct))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = direct });
                    yield break;
                }
                yield return Result<UnifiedChatChunk>.Failure("LLM 未返回有效的 tool_calls 或直接回答。");
                yield break;
            }

            // 执行工具（简化：不做重试/校验，S2 最小实现）
            var argsDict = new Dictionary<string, object>();
            string parseError = null;
            try
            {
                var argsStr = call.Function?.Arguments;
                if (!string.IsNullOrWhiteSpace(argsStr) && argsStr != "{}")
                {
                    var jObj = JObject.Parse(argsStr);
                    argsDict = jObj.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }
            }
            catch (System.Exception ex) { parseError = $"解析 tool 参数失败: {ex.Message}"; }
            if (parseError != null)
            {
                yield return Result<UnifiedChatChunk>.Failure(parseError);
                yield break;
            }

            object toolResult = null;
            IAsyncEnumerable<Result<UnifiedChatChunk>> errorStream = null;
            try { toolResult = await _tools.ExecuteToolAsync(call.Function.Name, argsDict); }
            catch (System.Exception ex)
            {
                var errMessages = new List<ChatMessage>
                {
                    new ChatMessage{ Role = "system", Content = systemPrompt },
                    new ChatMessage{ Role = "user", Content = query },
                    new ChatMessage{ Role = "assistant", Content = $"调用工具 {call.Function.Name} 失败: {ex.Message}" }
                };
                var errReq = new UnifiedChatRequest { Stream = true, Messages = errMessages };
                errorStream = _llm.StreamResponseAsync(errReq);
            }
            if (errorStream != null)
            {
                await foreach (var chunk in errorStream) yield return chunk;
                yield break;
            }

            var toolResultJson = Newtonsoft.Json.JsonConvert.SerializeObject(toolResult, Newtonsoft.Json.Formatting.None);
            var followMessages = new List<ChatMessage>
            {
                new ChatMessage{ Role = "system", Content = systemPrompt },
                new ChatMessage{ Role = "user", Content = query },
                new ChatMessage{ Role = "assistant", ToolCalls = new List<ToolCall>{ call } },
                new ChatMessage{ Role = "tool", ToolCallId = call.Id, Content = toolResultJson }
            };
            var followReq = new UnifiedChatRequest { Stream = true, Tools = toolDefinitions, Messages = followMessages };
            await foreach (var chunk in _llm.StreamResponseAsync(followReq)) yield return chunk;
        }

        private static string BuildInjectedContext(IReadOnlyList<RagHit> hits)
        {
            if (hits == null || hits.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            sb.AppendLine("以下为与当前请求高度相关的内部上下文，请尽量引用要点：");
            foreach (var h in hits.Take(5))
            {
                sb.AppendLine($"- [{h.DocId}] {Truncate(h.Content, 200)} (score={h.Score:F2})");
            }
            return sb.ToString();
        }

        private static string CombineSystemPrompt(string persona, string injected)
        {
            if (string.IsNullOrWhiteSpace(injected)) return persona ?? string.Empty;
            if (string.IsNullOrWhiteSpace(persona)) return injected;
            return persona + "\n\n" + injected;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? string.Empty;
            return s.Substring(0, max) + "…";
        }
    }
}


