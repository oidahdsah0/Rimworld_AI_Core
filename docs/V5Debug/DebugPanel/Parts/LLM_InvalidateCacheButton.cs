using RimAI.Core.Source.Modules.LLM;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class LLM_InvalidateCacheButton
	{
		private static string _convId = string.Empty;

		public static void Draw(Listing_Standard listing, ILLMService llm)
		{
			listing.Label("[P2] Invalidate Conversation Cache:");
			_convId = listing.TextEntry(_convId ?? string.Empty, 1);
			if (listing.ButtonText("Invalidate"))
			{
				_ = System.Threading.Tasks.Task.Run(async () => await RunAsync(llm, _convId));
			}
		}

		private static async System.Threading.Tasks.Task RunAsync(ILLMService llm, string convId)
		{
			var r = await llm.InvalidateConversationCacheAsync(convId);
			if (r.IsSuccess && r.Value)
			{
				Log.Message($"[RimAI.Core][P2.LLM] invalidate ok conv={convId}");
			}
			else
			{
				Log.Warning($"[RimAI.Core][P2.LLM] invalidate failed conv={convId} err={r.Error}");
			}
		}
	}
}


