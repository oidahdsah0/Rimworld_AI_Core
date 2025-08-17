using System.Threading.Tasks;
using Verse;
using RimAI.Core.Source.Modules.LLM;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
    internal static class LLM_PingButton
    {
        public static void Draw(Listing_Standard listing, ILLMService llm)
        {
            if (listing.ButtonText("[P2] Ping (non-stream)"))
            {
                _ = RunAsync(llm);
            }
        }

        private static async Task RunAsync(ILLMService llm)
        {
            var conv = "debug:ping";
            var req = new RimAI.Framework.Contracts.UnifiedChatRequest
            {
                ConversationId = conv,
                Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
                {
                    new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content="You are a helpful assistant." },
                    new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content="Say: pong" }
                },
                Stream = false
            };
            var resp = await llm.GetResponseAsync(req, default);
            Log.Message(resp.IsSuccess ? (resp.Value?.Message?.Content ?? "<empty>") : ("failed:" + resp.Error));
        }
    }
}


