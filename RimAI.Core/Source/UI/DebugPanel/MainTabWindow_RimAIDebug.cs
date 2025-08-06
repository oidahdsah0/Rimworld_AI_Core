using UnityEngine;
using RimWorld;
using Verse;
using RimAI.Core.Infrastructure;
using Newtonsoft.Json.Linq;
using RimAI.Framework.Contracts;
using RimAI.Core.Modules.World;
using System.Collections.Generic;
using System.Linq;

namespace RimAI.Core.UI.DebugPanel
{
    /// <summary>
    /// 开发者调试面板（P0 版本）。
    /// 仅包含一个 "Ping" 按钮，用于验证 Mod 与 DI 架构加载成功。
    /// </summary>
    public class MainTabWindow_RimAIDebug : MainTabWindow
    {
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 160f;
        private const float Padding = 10f;
        private const float OutputAreaHeight = 240f;
        private const int TotalButtons = 9;

        private string _output = string.Empty;
        private Vector2 _outputScroll = Vector2.zero;

        private void AppendOutput(string msg)
        {
            lock (this)
            {
                _output += $"[{System.DateTime.Now:HH:mm:ss}] {msg}\n";
            }
        }

        private const float ExtraWidth = 30f;
        public override Vector2 InitialSize => new(
            ButtonWidth * TotalButtons + Padding * (TotalButtons + 1) + ExtraWidth,
            (ButtonHeight + Padding * 3 + OutputAreaHeight) * 2.0f);

                public override void DoWindowContents(Rect inRect)
        {
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
                        AppendOutput($"Echo Response: {response} | Retries: {llm.LastRetries} | CacheHits: {llm.CacheHits}");
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
                        var colorTag = ms <= 1.0 ? "color=green" : "color=red";
                        AppendOutput($"<${colorTag}>Player Faction Name: {name} (\u03B4 {ms:F2} ms)</${colorTag}>");
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
                AppendOutput("Start Streaming...");
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await foreach (var chunk in llm.StreamResponseAsync(req))
                    {
                        if (chunk.IsSuccess)
                        {
                            var delta = chunk.Value?.ContentDelta;
                            if (!string.IsNullOrEmpty(delta))
                                AppendOutput(delta);
                            if (!string.IsNullOrEmpty(chunk.Value?.FinishReason))
                                AppendOutput($"[FINISH: {chunk.Value.FinishReason}]");
                        }
                        else
                        {
                            AppendOutput($"[Error] {chunk.Error}");
                        }
                    }
                });
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
                        int sum = Enumerable.Range(startVal, endVal - startVal + 1).Sum();

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
                var registry = CoreServices.Locator.Get<RimAI.Core.Modules.Tooling.IToolRegistryService>();
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

            // 2. 输出窗口 -----------------------------
            float outputY = curY + ButtonHeight + Padding;
            var outputRect = new Rect(inRect.x + Padding, outputY, inRect.width - 2 * Padding, OutputAreaHeight);

            var viewWidth = outputRect.width - 16f; // 考虑滚动条宽度
            var viewHeight = Mathf.Max(OutputAreaHeight, Text.CalcHeight(_output, viewWidth));
            var viewRect = new Rect(0, 0, viewWidth, viewHeight);

            Widgets.BeginScrollView(outputRect, ref _outputScroll, viewRect);
            Widgets.Label(viewRect, _output);
            Widgets.EndScrollView();
        }
    }
}