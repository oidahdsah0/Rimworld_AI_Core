using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class LlmJsonTestButton : IDebugPanelButton
    {
        public string Label => "LLM JSON Test";

        public void Execute(DebugPanelContext ctx)
        {
            Task.Run(async () =>
            {
                try
                {
                    var jreq = new UnifiedChatRequest
                    {
                        ForceJsonOutput = true,
                        Stream = false,
                        Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "请用 JSON 格式返回一个测试用的电商产品信息，包含产品名称、价格、描述、图片URL。" } }
                    };
                    jreq.ConversationId = $"debug:json:{System.Guid.NewGuid().ToString("N").Substring(0,8)}";
                    var jres = await RimAI.Framework.API.RimAIApi.GetCompletionAsync(jreq);
                    if (jres.IsSuccess)
                        ctx.AppendOutput($"JSON Response: {jres.Value.Message.Content}");
                    else
                        ctx.AppendOutput($"JSON Error: {jres.Error}");
                }
                catch (System.OperationCanceledException) { }
                catch (System.Exception ex)
                {
                    ctx.AppendOutput($"JSON Test failed: {ex.Message}");
                }
            });
        }
    }
}


