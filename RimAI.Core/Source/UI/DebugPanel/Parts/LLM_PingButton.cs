using System;
using RimAI.Core.Source.Modules.LLM;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class LLM_PingButton
	{
		public static void Draw(Listing_Standard listing, ILLMService llm)
		{
			if (listing.ButtonText("[P2] LLM Ping (non-stream)"))
			{
				var conv = "debug-p2-" + DateTime.UtcNow.Ticks;
				var start = DateTime.UtcNow;
				_ = DoPingAsync(llm, conv, start);
			}
		}

		private static async System.Threading.Tasks.Task DoPingAsync(ILLMService llm, string conv, DateTime start)
		{
			var resp = await llm.GetResponseAsync(conv, "You are a helpful assistant.", "Say: pong", default);
			if (resp.IsSuccess)
			{
				Log.Message($"[RimAI.Core][P2.LLM] ping ok conv={conv} elapsed={(DateTime.UtcNow-start).TotalMilliseconds:F0}ms finish={resp.Value?.FinishReason}");
			}
			else
			{
				Log.Error($"[RimAI.Core][P2.LLM] ping failed conv={conv} err={resp.Error}");
			}
		}
	}
}


