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
using RimAI.Core.Settings;
using RimAI.Core.Infrastructure;
using System;
using RimAI.Core.Modules.Orchestration.Planning;
using RimAI.Core.Contracts.Eventing;

namespace RimAI.Core.Modules.Orchestration.Strategies
{
    internal sealed class EmbeddingFirstStrategy : IOrchestrationStrategy
    {
        public string Name => "EmbeddingFirst";
        private readonly IEmbeddingService _embedding;
        private readonly IRagIndexService _rag;
        private readonly ILLMService _llm;
        private readonly IToolRegistryService _tools;
        private readonly RimAI.Core.Infrastructure.Configuration.IConfigurationService _config;
        private readonly IToolVectorIndexService _toolIndex;
        private readonly Planner _planner = new Planner();

        private readonly IPromptAssemblyService _promptAssembler;

        public EmbeddingFirstStrategy(IEmbeddingService embedding, IRagIndexService rag, ILLMService llm, IToolRegistryService tools,
            RimAI.Core.Infrastructure.Configuration.IConfigurationService config,
            IToolVectorIndexService toolIndex,
            IPromptAssemblyService promptAssembler)
        {
            _embedding = embedding;
            _rag = rag;
            _llm = llm;
            _tools = tools;
            _config = config;
            _toolIndex = toolIndex;
            _promptAssembler = promptAssembler;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context)
        {
            var query = context.Query ?? string.Empty;
            var persona = context.PersonaSystemPrompt ?? string.Empty;
            try
            {
                var assembled = await _promptAssembler.BuildSystemPromptAsync(System.Array.Empty<string>());
                if (!string.IsNullOrWhiteSpace(assembled))
                {
                    persona = string.IsNullOrWhiteSpace(persona) ? assembled : (assembled + "\n" + persona);
                }
            }
            catch { /* ignore */ }

            // Step 0: RAG 预处理
            float[] qv = null;
            string failure = null;
            try { qv = await _embedding.GetEmbeddingAsync(query); }
            catch (System.Exception ex) { failure = $"Embedding 失败: {ex.Message}"; }
            // RAG 失败不直接中断，降级为无 RAG 上下文继续

            var hits = (qv != null) ? await _rag.QueryAsync(qv, topK: 5) : new List<RagHit>();
            if (hits != null && hits.Count > 0)
            {
                var preview = string.Join(", ", hits.Take(5).Select(h => $"{h.DocId}:{h.Score:F2}"));
                RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[EmbeddingFirst] RAG hits: {preview}");
            }
            var injectedContext = BuildInjectedContext(hits);

            // 工具匹配模式（Classic/NarrowTopK/FastTop1/LightningFast）
            var allSchemas = _tools.GetAllToolSchemas();
            var (mode, toolSchemas, selectedToolName, fastResponse) = await SelectToolsAsync(query, allSchemas);

            // 构造 tools 定义
            var toolDefinitions = toolSchemas.Select(schema => new ToolDefinition
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
            // LightningFast：为工具注入快速响应参数
            if (fastResponse)
            {
                if (!argsDict.ContainsKey("__fastResponse")) argsDict["__fastResponse"] = true;
            }
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

            // LightningFast：若工具返回字符串，直接流式输出
            if (fastResponse && toolResult is string s && !string.IsNullOrEmpty(s))
            {
                foreach (var part in SliceString(s, 120))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = part });
                    await Task.Yield();
                }
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
            // 若启用轻量规划器，则在系统提示中加入汇总的 final_prompt（不改变外部接口，仅增强 system 提示）
            var planning = _config?.Current?.Orchestration?.Planning;
            if (planning?.EnableLightChaining == true)
            {
                var ragSnippets = hits?.Select(h => h.Content);
                var toolSummaries = new[] { Truncate(toolResultJson, 400) };
                var result = await _planner.BuildFinalPromptAsync(query, systemPrompt, ragSnippets, toolSummaries, _config.Current.Embedding.MaxContextChars,
                    new EventBusPlanProgressReporter());
                followMessages[0] = new ChatMessage { Role = "system", Content = result.FinalPrompt };
                RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new RimAI.Core.Contracts.Eventing.OrchestrationProgressEvent
                {
                    Source = nameof(EmbeddingFirstStrategy),
                    Stage = "Planner",
                    Message = "已注入 final_prompt 到 system 提示",
                    PayloadJson = string.Empty
                });
            }

            var followReq = new UnifiedChatRequest { Stream = true, Tools = toolDefinitions, Messages = followMessages };
            await foreach (var chunk in _llm.StreamResponseAsync(followReq)) yield return chunk;
        }

        private async Task<(string mode, List<ToolFunction> schemas, string selectedTool, bool fastResponse)> SelectToolsAsync(string query, List<ToolFunction> all)
        {
            var cfg = _config?.Current;
            var toolsCfg = cfg?.Embedding?.Tools;
            var mode = toolsCfg?.Mode ?? "Classic";

            if (_toolIndex?.IsBuilding == true && (toolsCfg?.BlockDuringBuild ?? true))
            {
                RimAI.Core.Infrastructure.CoreServices.Logger.Warn("[ToolMatch][Classic] 索引构建中，降级 Classic（全工具暴露）。");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:Classic", Message = "索引构建中，降级 Classic" }); } catch { }
                return ("Classic", all, null, false);
            }

            double wName = toolsCfg?.ScoreWeights?.Name ?? 0.6;
            double wDesc = toolsCfg?.ScoreWeights?.Description ?? 0.4;
            if (_toolIndex == null)
                return ("Classic", all, null, false);

            async Task<(string m, List<ToolFunction> s, string sel, bool fr)> NarrowTopK()
            {
                var k = Math.Max(1, cfg?.Embedding?.TopK ?? 5);
                var matches = await _toolIndex.SearchAsync(query, all, k, wName, wDesc);
                if (matches == null || matches.Count == 0)
                    return ("Classic", all, null, false);
                var names = new HashSet<string>(matches.Select(m => m.Tool), StringComparer.OrdinalIgnoreCase);
                var schemas = all.Where(t => names.Contains(t.Name)).ToList();
                RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch][NarrowTopK] → {string.Join(", ", matches.Select(m => $"{m.Tool}:{m.Score:F2}"))}");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:NarrowTopK", Message = $"候选App：{string.Join(", ", matches.Select(m => m.Tool))}" }); } catch { }
                return ("NarrowTopK", schemas, null, false);
            }

            if (string.Equals(mode, "Classic", StringComparison.OrdinalIgnoreCase))
            {
                RimAI.Core.Infrastructure.CoreServices.Logger.Info("[ToolMatch][Classic] 暴露全部工具");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:Classic", Message = "暴露全部工具" }); } catch { }
                return ("Classic", all, null, false);
            }

            if (string.Equals(mode, "NarrowTopK", StringComparison.OrdinalIgnoreCase))
                return await NarrowTopK();

            var top1 = await _toolIndex.SearchTop1Async(query, all, wName, wDesc);
            var threshold = toolsCfg?.Top1Threshold ?? 0.82;
            var lightThreshold = toolsCfg?.LightningTop1Threshold ?? 0.86;

            if (string.Equals(mode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                if (top1 != null && top1.Score >= threshold)
                {
                    var schema = all.FirstOrDefault(t => string.Equals(t.Name, top1.Tool, StringComparison.OrdinalIgnoreCase));
                    if (schema != null)
                    {
                        RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch][Auto→FastTop1] Top1={top1.Tool} score={top1.Score:F2}");
                        try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:FastTop1", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                        return ("FastTop1", new List<ToolFunction> { schema }, top1.Tool, false);
                    }
                }
                return await NarrowTopK();
            }

            if (string.Equals(mode, "LightningFast", StringComparison.OrdinalIgnoreCase))
            {
                if (top1 != null && top1.Score >= lightThreshold)
                {
                    var schema = all.FirstOrDefault(t => string.Equals(t.Name, top1.Tool, StringComparison.OrdinalIgnoreCase));
                    if (schema != null)
                    {
                        RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch][LightningFast] Top1={top1.Tool} score={top1.Score:F2}");
                        try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:LightningFast", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                        return ("LightningFast", new List<ToolFunction> { schema }, top1.Tool, true);
                    }
                }
                mode = "FastTop1"; // 降级
            }

            if (string.Equals(mode, "FastTop1", StringComparison.OrdinalIgnoreCase))
            {
                if (top1 != null && top1.Score >= threshold)
                {
                    var schema = all.FirstOrDefault(t => string.Equals(t.Name, top1.Tool, StringComparison.OrdinalIgnoreCase));
                    if (schema != null)
                    {
                        RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch][FastTop1] Top1={top1.Tool} score={top1.Score:F2}");
                        try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch:FastTop1", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                        return ("FastTop1", new List<ToolFunction> { schema }, top1.Tool, false);
                    }
                }
                return await NarrowTopK();
            }

            return ("Classic", all, null, false);
        }

        private static IEnumerable<string> SliceString(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) yield break;
            for (int i = 0; i < s.Length; i += Math.Max(1, maxLen))
            {
                yield return s.Substring(i, Math.Min(maxLen, s.Length - i));
            }
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


