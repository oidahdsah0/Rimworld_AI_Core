using System;
using System.Threading.Tasks;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class ChatEchoButton : IDebugPanelButton
    {
        public string Label => "Chat Echo";

        public void Execute(DebugPanelContext ctx)
        {
            var llm = ctx.Get<RimAI.Core.Modules.LLM.ILLMService>();
            Task.Run(async () =>
            {
                try
                {
                    var response = await llm.GetResponseAsync("hello");
                    ctx.AppendOutput($"Echo Response: {response} | Retries: {llm.LastRetries}");
                }
                catch (Exception ex)
                {
                    ctx.AppendOutput($"Chat Echo failed: {ex.Message}");
                }
            });
        }
    }
}


