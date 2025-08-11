using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    internal sealed class LlmStreamTestButton : IDebugPanelButton
    {
        public string Label => "LLM Stream Test";

        public void Execute(DebugPanelContext ctx)
        {
            try
            {
                var llm = ctx.Get<RimAI.Core.Modules.LLM.ILLMService>();
                var req = new UnifiedChatRequest
                {
                    Stream = true,
                    Messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = "你好，简单介绍下Rimworld这个游戏，一句话，尽量简短。" } }
                };
                req.ConversationId = $"debug:stream:{System.Guid.NewGuid().ToString("N").Substring(0,8)}";
                Task.Run(async () => await ctx.HandleStreamingOutputAsync("LLM Stream Test", llm.StreamResponseAsync(req)));
            }
            catch (System.Exception ex)
            {
                ctx.AppendOutput($"LLM Stream Test failed: {ex.Message}");
            }
        }
    }
}


