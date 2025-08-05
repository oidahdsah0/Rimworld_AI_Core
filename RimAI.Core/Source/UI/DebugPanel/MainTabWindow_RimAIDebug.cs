using UnityEngine;
using RimWorld;
using Verse;
using RimAI.Core.Infrastructure;
using Newtonsoft.Json.Linq;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel
{
    /// <summary>
    /// 开发者调试面板（P0 版本）。
    /// 仅包含一个 "Ping" 按钮，用于验证 Mod 与 DI 架构加载成功。
    /// </summary>
    public class MainTabWindow_RimAIDebug : MainTabWindow
    {
                private const float ButtonHeight = 30f;
        private const float ButtonWidth = 180f;
        private const float Padding = 10f;

        public override Vector2 InitialSize => new(ButtonWidth + Padding * 2, ButtonHeight * 6 + Padding * 7);

        public override void DoWindowContents(Rect inRect)
        {
            var pingRect = new Rect(inRect.x + Padding, inRect.y + Padding, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(pingRect, "Ping"))
            {
                CoreServices.Logger.Info("Ping button clicked – DI container state: OK");
            }

                        var reloadRect = new Rect(inRect.x + Padding, inRect.y + Padding * 2 + ButtonHeight, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(reloadRect, "Reload Config"))
            {
                var configSvc = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                configSvc.Reload();
                CoreServices.Logger.Info($"Config Reloaded – New Temperature: {configSvc.Current.LLM.Temperature}");
            }

            var startY = inRect.y + Padding * 3 + ButtonHeight * 2;
            var echoRect = new Rect(inRect.x + Padding, startY, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(echoRect, "Chat Echo"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var response = llm.GetResponseAsync("hello").GetAwaiter().GetResult();
                CoreServices.Logger.Info($"Echo Response: {response} | Retries: {llm.LastRetries} | CacheHits: {llm.CacheHits}");
            }

            // Stream Test
            var streamRect = new Rect(inRect.x + Padding, startY + (ButtonHeight + Padding), ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(streamRect, "LLM Stream Test"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var req = new UnifiedChatRequest
                {
                    Stream = true,
                    Messages = new System.Collections.Generic.List<ChatMessage> { new ChatMessage { Role = "user", Content = "你好，分段返回这句话。" } }
                };
                CoreServices.Logger.Info("Start Streaming...");
                System.Threading.Tasks.Task.Run(async () =>
                {
                    await foreach (var chunk in llm.StreamResponseAsync(req))
                    {
                        if (chunk.IsSuccess && chunk.Value.ContentDelta != null)
                            CoreServices.Logger.Info($"Stream Δ: {chunk.Value.ContentDelta}");
                    }
                });
            }

            // JSON Test
            var jsonRect = new Rect(inRect.x + Padding, startY + (ButtonHeight + Padding) * 2, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(jsonRect, "LLM JSON Test"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var jsonResp = llm.GetResponseAsync("请用 JSON 格式返回 {\"key\":\"value\"}", true).GetAwaiter().GetResult();
                CoreServices.Logger.Info($"JSON Response: {jsonResp}");
            }

            // Tools Test
            var toolsRect = new Rect(inRect.x + Padding, startY + (ButtonHeight + Padding) * 3, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(toolsRect, "LLM Tools Test"))
            {
                var llm = CoreServices.Locator.Get<RimAI.Core.Modules.LLM.ILLMService>();
                                var functionObj = new JObject
                {
                    ["name"] = "echo_tool",
                    ["description"] = "Echo text",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["text"] = new JObject { ["type"] = "string" }
                        }
                    }
                };
                var toolDef = new ToolDefinition { Function = functionObj };
                var req = new UnifiedChatRequest
                {
                    Messages = new System.Collections.Generic.List<ChatMessage> { new ChatMessage { Role = "user", Content = "使用 echo_tool 重复这句话: 你好" } },
                    Tools = new System.Collections.Generic.List<ToolDefinition> { toolDef }
                };
                var result = llm.GetResponseAsync("使用 echo_tool 重复这句话: 你好").GetAwaiter().GetResult();
                CoreServices.Logger.Info($"Tools Test Response: {result}");
            }

            // Batch Test
            var batchRect = new Rect(inRect.x + Padding, startY + (ButtonHeight + Padding) * 4, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(batchRect, "LLM Batch Test"))
            {
                CoreServices.Logger.Info("Batch Test not implemented in P2 demo – placeholder.");
            }
        }
    }
}