using System;
using System.Text;
using RimAI.Core.Source.Modules.LLM;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class LLM_StreamDemoButton
	{
		public static void Draw(Listing_Standard listing, ILLMService llm)
		{
			if (listing.ButtonText("[P2] LLM Stream Demo"))
			{
				_ = RunStreamAsync(llm);
			}
		}

		private static async System.Threading.Tasks.Task RunStreamAsync(ILLMService llm)
		{
			var conv = "debug-stream-" + DateTime.UtcNow.Ticks;
			var sb = new StringBuilder();
			await foreach (var r in llm.StreamResponseAsync(conv, "You are a helpful assistant.", "请用中文给我讲一个关于机器人和猫的短笑话。"))
			{
				if (!r.IsSuccess)
				{
					Log.Error($"[RimAI.Core][P2.LLM] stream error: {r.Error}");
					break;
				}
				var chunk = r.Value;
				if (chunk.ContentDelta != null)
				{
					sb.Append(chunk.ContentDelta);
				}
				if (!string.IsNullOrEmpty(chunk.FinishReason))
				{
					Log.Message($"[RimAI.Core][P2.LLM] stream end finish={chunk.FinishReason} text={sb}");
				}
			}
		}
	}
}


