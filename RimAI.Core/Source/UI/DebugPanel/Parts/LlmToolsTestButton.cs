using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class LlmToolsTestButton : IDebugPanelButton
    {
        public string Label => "LLM Tools Test";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.AppendOutput("[提示] 此测试使用临时 function 工具 sum_range（非注册），直接走 LLM Tools 接口，不经过编排/匹配模式与索引，设置页对其不生效。");
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
                Type = "function",
                Function = functionObj
            };
            var baseConvId = $"debug:tools:{System.Guid.NewGuid().ToString("N").Substring(0,8)}";
            var req = new UnifiedChatRequest
            {
                Messages = new System.Collections.Generic.List<ChatMessage> { new ChatMessage { Role = "user", Content = "请使用 sum_range 工具计算 1 到 100 的和。", ToolCalls = null } },
                Tools = new System.Collections.Generic.List<ToolDefinition> { toolDef }
            };
            req.ConversationId = baseConvId;

            Task.Run(async () =>
            {
                try
                {
                    var chatRes1 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(req);
                    if (!chatRes1.IsSuccess)
                    {
                        ctx.AppendOutput($"Tools Test Error: {chatRes1.Error}");
                        return;
                    }

                    var toolCall = chatRes1.Value.Message.ToolCalls?.FirstOrDefault();
                    if (toolCall == null)
                    {
                        ctx.AppendOutput("Tools Test Error: model did not return tool_calls.");
                        return;
                    }

                    var args = JObject.Parse(toolCall.Function?.Arguments ?? "{}");
                    int startVal = args["start"]?.Value<int>() ?? 0;
                    int endVal = args["end"]?.Value<int>() ?? 0;
                    int sum = System.Math.Max(0, endVal - startVal + 1) == 0 ? 0 : Enumerable.Range(startVal, endVal - startVal + 1).Sum();

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
                    followReq.ConversationId = baseConvId;

                    var chatRes2 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(followReq);
                    if (chatRes2.IsSuccess)
                        ctx.AppendOutput($"Tools Test Response: {chatRes2.Value.Message.Content}");
                    else
                        ctx.AppendOutput($"Tools Test Error: {chatRes2.Error}");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Tools Test failed: {ex.Message}");
                }
            });
        }
    }
}


