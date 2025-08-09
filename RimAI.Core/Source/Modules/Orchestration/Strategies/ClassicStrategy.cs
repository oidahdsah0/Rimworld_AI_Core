using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Tooling;
// using RimAI.Core.Infrastructure.Cache; // 缓存已下沉至 Framework
using RimAI.Core.Modules.LLM;
using RimAI.Framework.Contracts;
using RimAI.Core.Settings;
using RimAI.Core.Infrastructure;
using System;
using RimAI.Core.Modules.Orchestration.Planning;
using RimAI.Core.Contracts.Eventing;

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
        
        private readonly IPersonaService _personaService;
        private readonly Infrastructure.Configuration.IConfigurationService _config;
        private readonly Modules.Embedding.IToolVectorIndexService _toolIndex;
        private readonly Planner _planner = new Planner();
        private readonly IPromptAssemblyService _promptAssembler;

        public ClassicStrategy(ILLMService llm, IToolRegistryService tools, IPersonaService personaService,
            Infrastructure.Configuration.IConfigurationService config,
            Modules.Embedding.IToolVectorIndexService toolIndex,
            IPromptAssemblyService promptAssembler)
        {
            _llm = llm;
            _tools = tools;
            _personaService = personaService;
            _config = config;
            _toolIndex = toolIndex;
            _promptAssembler = promptAssembler;
        }

        public async IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context)
        {
            var query = context.Query ?? string.Empty;
            var personaSystemPrompt = context.PersonaSystemPrompt ?? string.Empty;

            // P10-M1: 预留提示组装调用（当前返回空字符串，不改变行为）
            try
            {
                var assembled = await _promptAssembler.BuildSystemPromptAsync(System.Array.Empty<string>());
                if (!string.IsNullOrWhiteSpace(assembled))
                {
                    personaSystemPrompt = string.IsNullOrWhiteSpace(personaSystemPrompt) ? assembled : (assembled + "\n" + personaSystemPrompt);
                }
            }
            catch { /* ignore */ }

            if (string.IsNullOrWhiteSpace(personaSystemPrompt))
            {
                var def = _personaService.Get("Default");
                if (def != null)
                    personaSystemPrompt = def.SystemPrompt;
            }

            // Step 0: 工具匹配模式（Classic/NarrowTopK/FastTop1/LightningFast）
            var allSchemas = _tools.GetAllToolSchemas();
            var (mode, toolDefinitions, selectedToolName, fastResponse) = await SelectToolsAsync(query, allSchemas);

            // 若 LightningFast 且有直接文本结果，已在 SelectToolsAsync 中处理返回

            // 构造 tools definition 列表供 LLM 决策
            var toolDefs = toolDefinitions.Select(schema => new ToolDefinition
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
                Tools  = toolDefs,
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
                // LightningFast：为工具注入快速响应参数
                if (fastResponse)
                {
                    if (!argsDict.ContainsKey("__fastResponse")) argsDict["__fastResponse"] = true;
                }
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

            // LightningFast：若工具返回字符串，直接流式输出（跳过二次 LLM 总结）
            if (fastResponse && toolResult is string s && !string.IsNullOrEmpty(s))
            {
                foreach (var part in SliceString(s, 120))
                {
                    yield return Result<UnifiedChatChunk>.Success(new UnifiedChatChunk { ContentDelta = part });
                    await Task.Yield();
                }
                yield break;
            }

            // --- 发起流式请求 ---
            var toolResultJson = JsonConvert.SerializeObject(toolResult, Formatting.None);
            var followMessages = BuildBaseMessages(personaSystemPrompt, query);
            followMessages.Add(new ChatMessage { Role = "assistant", ToolCalls = new List<ToolCall> { call } });
            followMessages.Add(new ChatMessage { Role = "tool", ToolCallId = call.Id, Content = toolResultJson });

            // 若启用轻量规划器，则增强 system 提示为 final_prompt
            var planning = _config?.Current?.Orchestration?.Planning;
            if (planning?.EnableLightChaining == true)
            {
                var toolSummaries = new[] { toolResultJson };
                var result = await _planner.BuildFinalPromptAsync(query, personaSystemPrompt, null, toolSummaries, _config.Current.Embedding.MaxContextChars,
                    new EventBusPlanProgressReporter());
                followMessages[0] = new ChatMessage { Role = "system", Content = result.FinalPrompt };
                CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                {
                    Source = nameof(ClassicStrategy),
                    Stage = "Planner",
                    Message = "已注入 final_prompt 到 system 提示",
                    PayloadJson = string.Empty
                });
            }

            var followReq = new UnifiedChatRequest
            {
                Stream = true,
                Tools = toolDefs,
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
        }

        private async Task<(string mode, List<ToolFunction> schemas, string selectedTool, bool fastResponse)> SelectToolsAsync(string query, List<ToolFunction> all)
        {
            var cfg = _config?.Current;
            var toolsCfg = cfg?.Embedding?.Tools;
            var mode = toolsCfg?.Mode ?? "Classic";

            // 构建期间阻断处理
            if (_toolIndex?.IsBuilding == true && (toolsCfg?.BlockDuringBuild ?? true))
            {
                CoreServices.Logger.Warn("[ToolMatch] 索引构建中，降级 Classic（全工具暴露）。");
                return ("Classic", all, null, false);
            }

            // 统一权重
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
                CoreServices.Logger.Info($"[ToolMatch] NarrowTopK → {string.Join(", ", matches.Select(m => $"{m.Tool}:{m.Score:F2}"))}");
                // 进度反馈：候选工具列表
                try
                {
                    var bus = CoreServices.Locator.Get<IEventBus>();
                    var payload = matches.Select(m => new { m.Tool, Score = m.Score }).ToList();
                    bus?.Publish(new OrchestrationProgressEvent
                    {
                        Source = nameof(ClassicStrategy),
                        Stage = "ToolMatch",
                        Message = $"候选App：{string.Join(", ", matches.Select(m => m.Tool))}",
                        PayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(payload)
                    });
                }
                catch { }
                return ("NarrowTopK", schemas, null, false);
            }

            if (string.Equals(mode, "Classic", StringComparison.OrdinalIgnoreCase))
                return ("Classic", all, null, false);

            if (string.Equals(mode, "NarrowTopK", StringComparison.OrdinalIgnoreCase))
                return await NarrowTopK();

            // FastTop1 / LightningFast
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
                        CoreServices.Logger.Info($"[ToolMatch] Auto→FastTop1 Top1={top1.Tool} score={top1.Score:F2}");
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
                        CoreServices.Logger.Info($"[ToolMatch] LightningFast Top1={top1.Tool} score={top1.Score:F2}");
                        // 进度反馈：命中Top1
                        try
                        {
                            CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                            {
                                Source = nameof(ClassicStrategy),
                                Stage = "ToolMatch",
                                Message = $"找到一个可用App：{schema.Name}"
                            });
                        }
                        catch { }
                        return ("LightningFast", new List<ToolFunction> { schema }, top1.Tool, true);
                    }
                }
                // 降级到 FastTop1
                mode = "FastTop1";
            }

            if (string.Equals(mode, "FastTop1", StringComparison.OrdinalIgnoreCase))
            {
                if (top1 != null && top1.Score >= threshold)
                {
                    var schema = all.FirstOrDefault(t => string.Equals(t.Name, top1.Tool, StringComparison.OrdinalIgnoreCase));
                    if (schema != null)
                    {
                        CoreServices.Logger.Info($"[ToolMatch] FastTop1 Top1={top1.Tool} score={top1.Score:F2}");
                        // 明确记录 FastTop1 命中并将要走“单工具暴露”路径
                        try
                        {
                            CoreServices.Logger.Info($"[ToolMatch] FastTop1 命中：将仅暴露工具 {schema.Name} 给 LLM 决策");
                        }
                        catch { }
                        // 进度反馈：命中Top1
                        try
                        {
                            CoreServices.Locator.Get<IEventBus>()?.Publish(new OrchestrationProgressEvent
                            {
                                Source = nameof(ClassicStrategy),
                                Stage = "ToolMatch",
                                Message = $"找到一个可用App：{schema.Name}"
                            });
                        }
                        catch { }
                        return ("FastTop1", new List<ToolFunction> { schema }, top1.Tool, false);
                    }
                }
                // 降级到 NarrowTopK
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

        
    }
}


