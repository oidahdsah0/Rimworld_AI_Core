using UnityEngine;
using RimWorld;
using Verse;
using RimAI.Core.Infrastructure;
using Newtonsoft.Json.Linq;
using RimAI.Framework.Contracts;
using RimAI.Core.Modules.World;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Eventing;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace RimAI.Core.UI.DebugPanel
{
    /// <summary>
    /// 开发者调试面板（P0 版本）。
    /// 仅包含一个 "Ping" 按钮，用于验证 Mod 与 DI 架构加载成功。
    /// </summary>
    public class MainTabWindow_RimAIDebug : MainTabWindow
    {
        // A simple event implementation for testing purposes.
        private class TestEvent : IEvent
        {
            public string Id { get; } = System.Guid.NewGuid().ToString();
            public System.DateTime Timestamp { get; } = System.DateTime.UtcNow;
            public EventPriority Priority { get; }
            private readonly string _description;

            public TestEvent(EventPriority priority, string description)
            {
                Priority = priority;
                _description = description;
            }

            public string Describe() => _description;
        }

        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 160f;
        private const float Padding = 10f;
        private const float OutputAreaHeight = 380f;
        private const int TotalButtons = 10;

        private readonly System.Text.StringBuilder _outputSb = new System.Text.StringBuilder();
        private Vector2 _outputScroll = Vector2.zero;
        private readonly ConcurrentQueue<string> _pendingChunks = new();
        private bool _subscribed;

        private void AppendOutput(string msg)
        {
            _pendingChunks.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        
        /// <summary>
        /// 处理流式输出的辅助函数
        /// </summary>
        private async Task HandleStreamingOutputAsync(string streamName, IAsyncEnumerable<Result<UnifiedChatChunk>> stream)
        {
            try
            {
                _pendingChunks.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {streamName}: "); // 开始流式输出，不加换行符

                if (stream == null)
                {
                    _pendingChunks.Enqueue("[Error] stream is null\n");
                    return;
                }

                string finalFinishReason = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await foreach (var chunk in stream)
                {
                    if (chunk.IsSuccess)
                    {
                        var delta = chunk.Value?.ContentDelta;
                        if (!string.IsNullOrEmpty(delta))
                        {
                            _pendingChunks.Enqueue(delta);
                        }
                        if (!string.IsNullOrEmpty(chunk.Value?.FinishReason))
                        {
                            finalFinishReason = chunk.Value.FinishReason;
                        }
                    }
                    else
                    {
                        _pendingChunks.Enqueue($"[Error] {chunk.Error}");
                    }
                }
                sw.Stop();

                _pendingChunks.Enqueue("\n"); // 流式传输结束后换行

                if (finalFinishReason != null)
                {
                    _pendingChunks.Enqueue($"[FINISH: {finalFinishReason}] (耗时: {sw.Elapsed.TotalSeconds:F2} s)\n");
                }
            }
            catch (System.Exception ex)
            {
                _pendingChunks.Enqueue($"{streamName} failed: {ex.Message}\n");
            }
        }


        private const float ExtraWidth = 30f;
        public override Vector2 InitialSize => new(
            ButtonWidth * TotalButtons + Padding * (TotalButtons + 1) + ExtraWidth,
            (ButtonHeight + Padding * 3 + OutputAreaHeight) * 1.2f);

                public override void DoWindowContents(Rect inRect)
        {
            // 将后台线程生成的增量 flush 到输出
            while (_pendingChunks.TryDequeue(out var part))
            {
                _outputSb.Append(part);
            }

            // 订阅编排进度事件（一次性）
            if (!_subscribed)
            {
                _subscribed = true;
                var bus = CoreServices.Locator.Get<IEventBus>();
                bus?.Subscribe<IEvent>(evt =>
                {
                    try
                    {
                        var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                        var pc = cfg?.Current?.Orchestration?.Progress;
                        string template = pc?.DefaultTemplate ?? "[{Source}] {Stage}: {Message}";
                        string source = null, stage = null, message = evt.Describe();
                        string payload = null;
                        var t = evt.GetType();
                        var pStage = t.GetProperty("Stage");
                        var pSource = t.GetProperty("Source");
                        var pMessage = t.GetProperty("Message");
                        var pPayload = t.GetProperty("PayloadJson");
                        if (pStage != null) stage = pStage.GetValue(evt) as string;
                        if (pSource != null) source = pSource.GetValue(evt) as string;
                        if (pMessage != null) message = pMessage.GetValue(evt) as string ?? message;
                        if (pc?.StageTemplates != null && stage != null && pc.StageTemplates.TryGetValue(stage, out var st))
                            template = st;
                        string line = template
                            .Replace("{Source}", source ?? string.Empty)
                            .Replace("{Stage}", stage ?? string.Empty)
                            .Replace("{Message}", message ?? string.Empty);
                        _pendingChunks.Enqueue($"[Progress] {line}\n");
                        if (pPayload != null)
                        {
                            payload = pPayload.GetValue(evt) as string;
                            int max = System.Math.Max(0, pc?.PayloadPreviewChars ?? 200);
                            if (!string.IsNullOrEmpty(payload))
                            {
                                if (payload.Length > max) payload = payload.Substring(0, max) + "…";
                                _pendingChunks.Enqueue($"  payload: {payload}\n");
                            }
                        }
                    }
                    catch
                    {
                        _pendingChunks.Enqueue($"[Progress] {evt.Describe()}\n");
                    }
                });
            }

            // 1. 顶部横向按钮 -----------------------------
            float curX = inRect.x + Padding;
            float curY = inRect.y + Padding;

            // 本地函数用于创建按钮并推进 X 坐标
            bool Button(string label)
            {
                var rect = new Rect(curX, curY, ButtonWidth, ButtonHeight);
                bool clicked = Widgets.ButtonText(rect, label);
                curX += ButtonWidth + Padding;
                return clicked;
            }

            // Ping
            if (Button("Ping"))
            {
                AppendOutput("Ping button clicked – DI container state: OK");
            }

            // Reload Config
            if (Button("Reload Config"))
            {
                var configSvc = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                configSvc.Reload();
                AppendOutput($"Config Reloaded – New Temperature: {configSvc.Current.LLM.Temperature}");
            }

            // Chat Echo
            if (Button("Chat Echo"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var response = await llm.GetResponseAsync("hello");
                        AppendOutput($"Echo Response: {response} | Retries: {llm.LastRetries}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Chat Echo failed: {ex.Message}");
                    }
                });
            }

            // Get Player Name (P3)
            if (Button("Get Player Name"))
            {
                var world = CoreServices.Locator.Get<IWorldDataService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var name = await world.GetPlayerNameAsync();
                        sw.Stop();
                        var ms = sw.Elapsed.TotalMilliseconds;
                        var green = ms <= 1.0;
                        var tagOpen = green ? "<color=green>" : "<color=red>";
                        var tagClose = "</color>";
                        AppendOutput($"{tagOpen}Player Faction Name: {name} (Δ {ms:F2} ms){tagClose}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Get Player Name failed: {ex.Message}");
                    }
                });
            }

            // LLM Stream Test
            if (Button("LLM Stream Test"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var req = new UnifiedChatRequest
                {
                    Stream = true,
                    Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "你好，简单介绍下Rimworld这个游戏，一句话，尽量简短。" } }
                };
                
                System.Threading.Tasks.Task.Run(async () => await HandleStreamingOutputAsync("LLM Stream Test", llm.StreamResponseAsync(req)));
            }

            // JSON Test
            if (Button("LLM JSON Test"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var jreq = new UnifiedChatRequest
                        {
                            ForceJsonOutput = true,
                            Stream = false,
                            Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "请用 JSON 格式返回一个测试用的电商产品信息，包含产品名称、价格、描述、图片URL。" } }
                        };
                        var jres = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(jreq);
                        if (jres.IsSuccess)
                            AppendOutput($"JSON Response: {jres.Value.Message.Content}");
                        else
                            AppendOutput($"JSON Error: {jres.Error}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"JSON Test failed: {ex.Message}");
                    }
                });
            }

            // Tools Test
            if (Button("LLM Tools Test"))
            {
                AppendOutput("[提示] 此测试使用临时 function 工具 sum_range（非注册），直接走 LLM Tools 接口，不经过编排/匹配模式与索引，设置页对其不生效。");
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var functionObj = new JObject
                {
                    ["name"] = "sum_range",
                    ["description"] = "Calculate the sum of integers from start to end (inclusive)",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["start"] = new JObject { ["type"] = "integer", ["description"] = "Start number" },
                            ["end"] = new JObject { ["type"] = "integer", ["description"] = "End number" }
                        },
                        ["required"] = new JArray { "start", "end" }
                    }
                };
                var toolDef = new ToolDefinition
                {
                    Type = "function",  // 注意！必须指定类型为 function！！！
                    Function = functionObj
                };
                var req = new UnifiedChatRequest
                {
                    Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "请使用 sum_range 工具计算 1 到 100 的和。", ToolCalls = null } },
                    Tools = new System.Collections.Generic.List<ToolDefinition> { toolDef }
                };
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // Step 1: 发送带 tools 的初始请求
                        var chatRes1 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(req);
                        if (!chatRes1.IsSuccess)
                        {
                            AppendOutput($"Tools Test Error: {chatRes1.Error}");
                            return;
                        }

                        // 解析模型返回的 tool_calls
                        var toolCall = chatRes1.Value.Message.ToolCalls?.FirstOrDefault();
                        if (toolCall == null)
                        {
                            AppendOutput("Tools Test Error: model did not return tool_calls.");
                            return;
                        }

                        // 执行本地函数 sum_range
                        var args = JObject.Parse(toolCall.Function?.Arguments ?? "{}");
                        int startVal = args["start"]?.Value<int>() ?? 0;
                        int endVal = args["end"]?.Value<int>() ?? 0;
                        int sum = System.Math.Max(0, endVal - startVal + 1) == 0 ? 0 : Enumerable.Range(startVal, endVal - startVal + 1).Sum();

                        // Step 2: 构造带 tool 结果的跟进请求
                        var followReq = new UnifiedChatRequest
                        {
                            Messages = new List<ChatMessage>
                            {
                                new ChatMessage { Role = "user", Content = req.Messages[0].Content },
                                new ChatMessage { Role = "assistant", ToolCalls = new List<ToolCall> { toolCall } },
                                new ChatMessage { Role = "tool", ToolCallId = toolCall.Id, Content = sum.ToString() }
                            },
                            Tools = new List<ToolDefinition> { toolDef }
                        };

                        var chatRes2 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(followReq);
                        if (chatRes2.IsSuccess)
                            AppendOutput($"Tools Test Response: {chatRes2.Value.Message.Content}");
                        else
                            AppendOutput($"Tools Test Error: {chatRes2.Error}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Tools Test failed: {ex.Message}");
                    }
                });
            }

            // Run Tool (get_colony_status)
            if (Button("Run Tool"))
            {
                AppendOutput("[提示] 直接通过 IToolRegistryService 执行 get_colony_status（绕过编排），不受‘工具匹配模式/向量索引’影响。");
                var registry = CoreServices.Locator.Get<RimAI.Core.Contracts.Tooling.IToolRegistryService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var result = await registry.ExecuteToolAsync("get_colony_status", new System.Collections.Generic.Dictionary<string, object>());
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(result, Newtonsoft.Json.Formatting.None);
                        AppendOutput($"Colony Status: {json}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Run Tool failed: {ex.Message}");
                    }
                });
            }

            // Ask Colony Status (P5)
            if (Button("Ask Colony Status"))
            {
                try
                {
                    var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                    var mode = cfg?.Current?.Embedding?.Tools?.Mode ?? "Classic";
                    AppendOutput($"[提示] 通过编排服务执行，受设置页影响（模式={mode}，TopK/阈值/索引/动态阈值等）。");
                }
                catch { AppendOutput("[提示] 通过编排服务执行，受设置页影响。"); }
                var orchestrator = CoreServices.Locator.Get<RimAI.Core.Contracts.IOrchestrationService>();
                var query = "殖民地概况？";
                var stream = orchestrator.ExecuteToolAssistedQueryAsync(query);
                System.Threading.Tasks.Task.Run(async () => await HandleStreamingOutputAsync("Ask Colony Status", stream));
            }

            // Batch Test (5 greetings in different languages)
            if (Button("LLM Batch Test"))
            {
                var prompts = new List<string>
                {
                    "Hello!",
                    "你好！",
                    "¡Hola!",
                    "Bonjour!",
                    "こんにちは！"
                };
                var requests = new List<UnifiedChatRequest>();
                foreach (var p in prompts)
                {
                    requests.Add(new UnifiedChatRequest
                    {
                        Stream = false,
                        Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = p } }
                    });
                }

                System.Threading.Tasks.Task.Run(async () =>
                {
                    AppendOutput("Batch requesting...");
                    var results = await RimAI.Framework.API.RimAIApi.GetCompletionsAsync(requests);
                    for (int i = 0; i < results.Count; i++)
                    {
                        var res = results[i];
                        if (res.IsSuccess)
                            AppendOutput($"[{i}] {prompts[i]} -> {res.Value.Message.Content}");
                        else
                            AppendOutput($"[{i}] {prompts[i]} -> Error: {res.Error}");
                    }
                });
            }

            // 第二行按钮 -----------------------------
            curX = inRect.x + Padding;
            curY += ButtonHeight + Padding;

            if (Button("Record History"))
            {
                var history = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var participants = new List<string> { "__PLAYER__", "ColonyGovernor" };
                        await history.RecordEntryAsync(participants, new ConversationEntry("__PLAYER__", "测试对话：你好，总督！", System.DateTime.UtcNow));
                        await history.RecordEntryAsync(participants, new ConversationEntry("ColonyGovernor", "你好，指挥官！", System.DateTime.UtcNow));
                        AppendOutput("示例对话已写入；请手动存档→主菜单→读档后验证历史是否持久化。");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Record History failed: {ex.Message}");
                    }
                });
            }

            // Show History - 显示当前历史记录
            if (Button("Show History"))
            {
                var history = CoreServices.Locator.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>();
                var historyWrite = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
                var recap = CoreServices.Locator.Get<RimAI.Core.Modules.History.IRecapService>();
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var participants = new List<string> { "__PLAYER__", "ColonyGovernor" };
                        var context = await history.GetHistoryAsync(participants);
                        
                        AppendOutput($"=== 历史记录 ===");
                        AppendOutput($"主线对话数: {context.MainHistory.Count}");
                        
                        foreach (var conv in context.MainHistory)
                        {
                            AppendOutput($"对话条目数: {conv.Entries.Count}");
                            foreach (var entry in conv.Entries)
                            {
                                AppendOutput($"[{entry.Timestamp:HH:mm:ss}] {entry.SpeakerId}: {entry.Content}");
                            }
                        }
                        
                        if (context.MainHistory.Count == 0)
                        {
                            AppendOutput("没有找到历史记录。");
                        }

                        try
                        {
                            // 读取 Recap 计数调试信息
                            var convKey = string.Join("|", participants.OrderBy(x => x, System.StringComparer.Ordinal));
                            var n = recap?.GetCounter(convKey) ?? 0;
                            AppendOutput($"[调试] Recap 轮次计数（{convKey}）= {n}");
                        }
                        catch { /* ignore */ }
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Show History failed: {ex.Message}");
                    }
                });
            }

            // Open Persona Manager
            if (Button("Persona Manager"))
            {
                Find.WindowStack.Add(new RimAI.Core.UI.PersonaManager.MainTabWindow_PersonaManager());
            }

            // Open History Manager (P10-M3)
            if (Button("History Manager"))
            {
                try
                {
                    // 若存在 player:__SAVE__|pawn:DEMO 的会话键，优先打开该会话
                    string preset = "player:__SAVE__|pawn:DEMO";
                    Find.WindowStack.Add(new RimAI.Core.UI.HistoryManager.MainTabWindow_HistoryManager(preset));
                }
                catch (System.Exception ex)
                {
                    AppendOutput($"Open History Manager failed: {ex.Message}");
                }
            }

            // Dump Snapshot (P10-M3+)
            if (Button("Dump Snapshot"))
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var recap = CoreServices.Locator.Get<RimAI.Core.Modules.History.IRecapService>();
                        var fixedSvc = CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>();
                        var bioSvc = CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IBiographyService>();
                        var history = CoreServices.Locator.Get<RimAI.Core.Contracts.Services.IHistoryQueryService>();

                        var participants = new System.Collections.Generic.List<string> { "player:__SAVE__", "pawn:DEMO" };
                        var convKey = string.Join("|", participants.OrderBy(x => x, System.StringComparer.Ordinal));
                        var ctxTask = history.GetHistoryAsync(participants);
                        ctxTask.Wait();

                        AppendOutput($"[Snapshot] convKey={convKey}");
                        AppendOutput("- Fixed Prompts:");
                        foreach (var kv in fixedSvc.GetAll(convKey)) AppendOutput($"  {kv.Key}: {kv.Value}");
                        AppendOutput("- Biographies:");
                        foreach (var it in bioSvc.List(convKey)) AppendOutput($"  [{it.CreatedAt:HH:mm:ss}] {it.Text}");
                        AppendOutput("- Recap:");
                        foreach (var it in recap.GetRecapItems(convKey)) AppendOutput($"  [{it.CreatedAt:HH:mm:ss}] {it.Text}");
                        AppendOutput("- History (last entries):");
                        foreach (var c in ctxTask.Result.MainHistory)
                            foreach (var e in c.Entries) AppendOutput($"  [{e.Timestamp:HH:mm:ss}] {e.SpeakerId}: {e.Content}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput("Dump Snapshot failed: " + ex.Message);
                    }
                });
            }

            // Prompt Audit Test (M4)
            if (Button("Prompt Audit Test"))
            {
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        var conv = CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IPersonaConversationService>();
                        var participants = new System.Collections.Generic.List<string> { "player:__SAVE__", "pawn:DEMO" };
                        var stream = conv.ChatAsync(participants, "Default", "今天天气怎么样？", new RimAI.Core.Modules.Persona.PersonaChatOptions { Stream = true, WriteHistory = false });
                        await foreach (var chunk in stream)
                        {
                            if (chunk.IsSuccess)
                                _pendingChunks.Enqueue(chunk.Value?.ContentDelta ?? string.Empty);
                            else
                                _pendingChunks.Enqueue("[Error] " + chunk.Error);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput("Prompt Audit Test failed: " + ex.Message);
                    }
                });
            }

            // Colony FC Test
            if (Button("Colony FC Test"))
            {
                AppendOutput("[提示] 固定仅暴露 get_colony_status 的 function schema 给 LLM，不经过‘工具匹配模式’，设置页不生效（用于演示 LLM function-calling 流程）。");
                var registry = CoreServices.Locator.Get<RimAI.Core.Contracts.Tooling.IToolRegistryService>();
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();

                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 1. 获取工具 schema
                        var schema = registry.GetAllToolSchemas().FirstOrDefault(s => s.Name == "get_colony_status");
                        if (schema == null)
                        {
                            AppendOutput("Colony FC Error: schema not found.");
                            return;
                        }

                        // 2. 构造 OpenAI function definition
                        var functionObj = new JObject
                        {
                            ["name"] = schema.Name,
                            ["description"] = "获取殖民地状态的函数",
                            ["parameters"] = JObject.Parse(schema.Arguments ?? "{}")
                        };
                        var toolDef = new ToolDefinition { Type = "function", Function = functionObj };

                        // 3. 首次请求，征求 LLM 决策
                        var initReq = new UnifiedChatRequest
                        {
                            Stream = false,
                            Tools = new List<ToolDefinition> { toolDef },
                            Messages = new List<ChatMessage>
                            {
                                new ChatMessage { Role = "user", Content = "请获取殖民地当前概况并用一句中文总结。" }
                            }
                        };

                        var res1 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(initReq);
                        if (!res1.IsSuccess)
                        {
                            AppendOutput($"Colony FC Error: {res1.Error}");
                            return;
                        }

                        var call = res1.Value.Message.ToolCalls?.FirstOrDefault();
                        if (call == null)
                        {
                            AppendOutput("Colony FC Error: 模型未返回 tool_calls");
                            return;
                        }

                        // 4. 本地执行工具
                        var toolResult = await registry.ExecuteToolAsync(call.Function?.Name, new Dictionary<string, object>());
                        var toolJson = Newtonsoft.Json.JsonConvert.SerializeObject(toolResult, Newtonsoft.Json.Formatting.None);

                        // 5. 跟进请求携带工具结果
                        var followReq = new UnifiedChatRequest
                        {
                            Messages = new List<ChatMessage>
                            {
                                new ChatMessage { Role = "user", Content = initReq.Messages[0].Content },
                                new ChatMessage { Role = "assistant", ToolCalls = new List<ToolCall> { call } },
                                new ChatMessage { Role = "tool", ToolCallId = call.Id, Content = toolJson }
                            },
                            Tools = initReq.Tools
                        };

                        var res2 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(followReq);
                        if (res2.IsSuccess)
                            AppendOutput($"Colony FC Response: {res2.Value.Message.Content}");
                        else
                            AppendOutput($"Colony FC Error: {res2.Error}");
                    }
                    catch (System.Exception ex)
                    {
                        AppendOutput($"Colony FC failed: {ex.Message}");
                    }
                });
            }
            
            if (Button("Trigger Test Events"))
            {
                var eventBus = CoreServices.Locator.Get<IEventBus>();
                System.Threading.Tasks.Task.Run(() =>
                {
                    AppendOutput("Publishing 5 test events (3 Low, 1 High, 1 Critical)...");
                    eventBus.Publish(new TestEvent(EventPriority.Low, "A trade caravan has arrived."));
                    eventBus.Publish(new TestEvent(EventPriority.Low, "A new colonist, 'Steve', has joined."));
                    eventBus.Publish(new TestEvent(EventPriority.High, "A psychic drone has started for female colonists."));
                    eventBus.Publish(new TestEvent(EventPriority.Low, "Component assembly finished."));
                    eventBus.Publish(new TestEvent(EventPriority.Critical, "A raid from the 'Savage Tribe' is attacking the colony."));
                    AppendOutput("Events published.");
                });
            }

            // 2. 输出窗口 -----------------------------
            float outputY = curY + ButtonHeight + Padding;
            var outputRect = new Rect(inRect.x + Padding, outputY, inRect.width - 2 * Padding, OutputAreaHeight);

            var viewWidth = outputRect.width - 16f; // 考虑滚动条宽度
            var viewHeight = Mathf.Max(OutputAreaHeight, Text.CalcHeight(_outputSb.ToString(), viewWidth));
            var viewRect = new Rect(0, 0, viewWidth, viewHeight);

            Widgets.BeginScrollView(outputRect, ref _outputScroll, viewRect);
            Widgets.Label(viewRect, _outputSb.ToString());
            Widgets.EndScrollView();
        }
    }
}
