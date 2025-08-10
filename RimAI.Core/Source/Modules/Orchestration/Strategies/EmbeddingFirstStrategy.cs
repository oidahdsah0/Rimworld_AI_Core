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
            var baseConvId = $"orc:{ComputeShortHash(persona + "\n" + query)}";
            // 策略层与 PersonaConversation 的职责边界：
            // 此处不组装 Chat/Command 素材，保留 personaSystemPrompt 透传。

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
            var toolDefinitions = toolSchemas.Select(schema =>
            {
                JObject parameters;
                var paramJson = string.IsNullOrWhiteSpace(schema?.Arguments) ? "{}" : schema.Arguments;
                try { parameters = JObject.Parse(paramJson); } catch { parameters = new JObject(); }
                return new ToolDefinition
                {
                    Type = "function",
                    Function = new JObject
                    {
                        ["name"] = schema?.Name ?? string.Empty,
                        ["description"] = schema?.Description ?? string.Empty,
                        ["parameters"] = parameters
                    }
                };
            }).ToList();

            // LightningFast 零参数优化：若命中且所选工具为零参数，直接本地执行并直出
            if (fastResponse && !string.IsNullOrWhiteSpace(selectedToolName))
            {
                string fastOut = null;
                try
                {
                    var selectedSchema = toolSchemas.FirstOrDefault(s => string.Equals(s?.Name, selectedToolName, System.StringComparison.OrdinalIgnoreCase));
                    var argJson = selectedSchema?.Arguments;
                    bool zeroArgs = false;
                    try
                    {
                        var jo = string.IsNullOrWhiteSpace(argJson) ? null : JObject.Parse(argJson);
                        var props = jo?["properties"] as JObject;
                        var required = jo?["required"] as JArray;
                        zeroArgs = (props == null || !props.Properties().Any()) && (required == null || !required.HasValues);
                    }
                    catch { zeroArgs = true; }

                    if (zeroArgs)
                    {
                        var fastArgs = new Dictionary<string, object> { ["__fastResponse"] = true };
                        object toolFastResult = await _tools.ExecuteToolAsync(selectedToolName, fastArgs);
                        // 记录 fastResponse 结果类型
                        try
                        {
                            RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                            {
                                Source = "Orchestrator",
                                Stage = "ToolMatch",
                                Message = $"LightningFast: 零参数直出，resultType={(toolFastResult == null ? "null" : toolFastResult.GetType().Name)}"
                            });
                        }
                        catch { }

                        if (toolFastResult is string sfast && !string.IsNullOrEmpty(sfast))
                        {
                            fastOut = sfast;
                        }
                        // 若工具未返回字符串，回退到常规流程
                    }
                }
                catch { /* 忽略，走常规流程 */ }

                if (!string.IsNullOrEmpty(fastOut))
                {
                    foreach (var part in SliceString(fastOut, 120))
                    {
                        yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = part });
                        await System.Threading.Tasks.Task.Yield();
                    }
                    yield break;
                }
            }

            // 将 RAG 上下文注入 system 提示
            var systemPrompt = CombineSystemPrompt(persona, injectedContext);
            var initMessages = new List<ChatMessage> { new ChatMessage { Role = "system", Content = systemPrompt }, new ChatMessage { Role = "user", Content = query } };
            var initReq = new UnifiedChatRequest { Stream = false, Tools = toolDefinitions, Messages = initMessages };
            initReq.ConversationId = baseConvId;

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

            // 统一进度提示：最终选择的工具
            try
            {
                RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                {
                    Source = "Orchestrator",
                    Stage = "ToolMatch",
                    Message = $"最终工具选择：{call.Function?.Name ?? selectedToolName ?? "(unknown)"}"
                });
            }
            catch { }

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
                errReq.ConversationId = baseConvId;
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
            followReq.ConversationId = baseConvId;
            await foreach (var chunk in _llm.StreamResponseAsync(followReq)) yield return chunk;
        }

        private static string ComputeShortHash(string input)
        {
            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
                    var hash = sha1.ComputeHash(bytes);
                    var sb = new System.Text.StringBuilder(20);
                    for (int i = 0; i < Math.Min(hash.Length, 10); i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "0000000000"; }
        }

        private async Task<(string mode, List<ToolFunction> schemas, string selectedTool, bool fastResponse)> SelectToolsAsync(string query, List<ToolFunction> all)
        {
            var cfg = _config?.Current;
            var toolsCfg = cfg?.Embedding?.Tools;
            var mode = toolsCfg?.Mode ?? "Classic";

            // 统一进度提示：当前匹配模式
            try
            {
                RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                {
                    Source = "Orchestrator",
                    Stage = "ToolMatch",
                    Message = $"当前匹配模式：{mode}"
                });
            }
            catch { }

            if (_toolIndex?.IsBuilding == true && (toolsCfg?.BlockDuringBuild ?? true))
            {
                RimAI.Core.Infrastructure.CoreServices.Logger.Warn("[ToolMatch] 索引构建中，降级 Classic（全工具暴露）。");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = "Orchestrator", Stage = "ToolMatch", Message = "索引构建中，降级 Classic（全工具暴露）" }); } catch { }
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
                    return ("NarrowTopK", all, null, false);
                var names = new HashSet<string>(matches.Select(m => m.Tool), StringComparer.OrdinalIgnoreCase);
                var schemas = all.Where(t => names.Contains(t.Name)).ToList();
                RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch] NarrowTopK → {string.Join(", ", matches.Select(m => $"{m.Tool}:{m.Score:F2}"))}");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = "Orchestrator", Stage = "ToolMatch", Message = $"候选工具：{string.Join(", ", matches.Select(m => m.Tool))}" }); } catch { }
                return ("NarrowTopK", schemas, null, false);
            }

            if (string.Equals(mode, "Classic", StringComparison.OrdinalIgnoreCase))
            {
                RimAI.Core.Infrastructure.CoreServices.Logger.Info("[ToolMatch] 暴露全部工具");
                try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = "Orchestrator", Stage = "ToolMatch", Message = "暴露全部工具" }); } catch { }
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
                        RimAI.Core.Infrastructure.CoreServices.Logger.Info($"[ToolMatch] Auto→FastTop1 Top1={top1.Tool} score={top1.Score:F2}");
                        try { RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = nameof(EmbeddingFirstStrategy), Stage = "ToolMatch", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                        return ("FastTop1", new List<ToolFunction> { schema }, top1.Tool, false);
                    }
                }
                return await NarrowTopK();
            }

            if (string.Equals(mode, "LightningFast", StringComparison.OrdinalIgnoreCase))
            {
                // 唯一模式不降级：忽略阈值，若有Top1则用Top1，否则回退首个工具
                var chosen = top1?.Tool ?? all.FirstOrDefault()?.Name;
                var schema = all.FirstOrDefault(t => string.Equals(t.Name, chosen, StringComparison.OrdinalIgnoreCase));
                if (schema != null)
                {
                    try { if (top1 != null) RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = "Orchestrator", Stage = "ToolMatch", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                    return ("LightningFast", new List<ToolFunction> { schema }, schema.Name, true);
                }
            }

            if (string.Equals(mode, "FastTop1", StringComparison.OrdinalIgnoreCase))
            {
                // 唯一模式不降级：忽略阈值，若有Top1则用Top1，否则回退首个工具
                var chosen = top1?.Tool ?? all.FirstOrDefault()?.Name;
                var schema = all.FirstOrDefault(t => string.Equals(t.Name, chosen, StringComparison.OrdinalIgnoreCase));
                if (schema != null)
                {
                    try { if (top1 != null) RimAI.Core.Infrastructure.CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent { Source = "Orchestrator", Stage = "ToolMatch", Message = $"命中Top1：{schema.Name} ({top1.Score:F2})" }); } catch { }
                    return ("FastTop1", new List<ToolFunction> { schema }, schema.Name, false);
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


