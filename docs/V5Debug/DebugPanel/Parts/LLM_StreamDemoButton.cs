using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.LLM;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
    internal static class LLM_StreamDemoButton
    {
        public static void Draw(Listing_Standard listing, ILLMService llm)
        {
            if (listing.ButtonText("[P2] Stream Demo (UI-only)"))
            {
                _ = RunAsync(llm);
            }
        }

        private static async Task RunAsync(ILLMService llm)
        {
            var conv = "debug:streamdemo";
            var req = new RimAI.Framework.Contracts.UnifiedChatRequest
            {
                ConversationId = conv,
                Messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
                {
                    new RimAI.Framework.Contracts.ChatMessage{ Role="system", Content="You are a helpful assistant."},
                    new RimAI.Framework.Contracts.ChatMessage{ Role="user", Content="请用中文给我讲一个关于机器人和猫的短笑话。"}
                },
                Stream = true
            };
            await foreach (var r in llm.StreamResponseAsync(req))
            {
                if (!r.IsSuccess) break;
                var chunk = r.Value;
                if (!string.IsNullOrEmpty(chunk.ContentDelta)) Log.Message(chunk.ContentDelta);
                if (!string.IsNullOrEmpty(chunk.FinishReason)) break;
            }
        }
    }
}


