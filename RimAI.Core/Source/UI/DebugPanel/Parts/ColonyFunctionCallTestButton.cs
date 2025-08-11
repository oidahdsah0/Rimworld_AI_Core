using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ColonyFunctionCallTestButton : IDebugPanelButton
    {
        public string Label => "Colony FC Test";

        public void Execute(DebugPanelContext ctx)
        {
            ctx.AppendOutput("[提示] 固定仅暴露 get_colony_status 的 function schema 给 LLM，不经过‘工具匹配模式’，设置页不生效（用于演示 LLM function-calling 流程）。");
            var registry = ctx.Get<RimAI.Core.Contracts.Tooling.IToolRegistryService>();
            var llm = ctx.Get<RimAI.Core.Modules.LLM.ILLMService>();

            Task.Run(async () =>
            {
                try
                {
                    var schema = registry.GetAllToolSchemas().FirstOrDefault(s => s.Name == "get_colony_status");
                    if (schema == null)
                    {
                        ctx.AppendOutput("Colony FC Error: schema not found.");
                        return;
                    }

                    var functionObj = new JObject
                    {
                        ["name"] = schema.Name,
                        ["description"] = "获取殖民地状态的函数",
                        ["parameters"] = JObject.Parse(schema.Arguments ?? "{}")
                    };
                    var toolDef = new ToolDefinition { Type = "function", Function = functionObj };

                    var baseConvId = $"debug:fc:{System.Guid.NewGuid().ToString("N").Substring(0,8)}";
                    var initReq = new UnifiedChatRequest
                    {
                        Stream = false,
                        Tools = new List<ToolDefinition> { toolDef },
                        Messages = new List<ChatMessage>
                        {
                            new ChatMessage { Role = "user", Content = "请获取殖民地当前概况并用一句中文总结。" }
                        }
                    };
                    initReq.ConversationId = baseConvId;

                    var res1 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(initReq);
                    if (!res1.IsSuccess)
                    {
                        ctx.AppendOutput($"Colony FC Error: {res1.Error}");
                        return;
                    }

                    var call = res1.Value.Message.ToolCalls?.FirstOrDefault();
                    if (call == null)
                    {
                        ctx.AppendOutput("Colony FC Error: 模型未返回 tool_calls");
                        return;
                    }

                    var toolResult = await registry.ExecuteToolAsync(call.Function?.Name, new Dictionary<string, object>());
                    var toolJson = JsonConvert.SerializeObject(toolResult, Formatting.None);

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
                    followReq.ConversationId = baseConvId;

                    var res2 = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(followReq);
                    if (res2.IsSuccess)
                        ctx.AppendOutput($"Colony FC Response: {res2.Value.Message.Content}");
                    else
                        ctx.AppendOutput($"Colony FC Error: {res2.Error}");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"Colony FC failed: {ex.Message}");
                }
            });
        }
    }
}


