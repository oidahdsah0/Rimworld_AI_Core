using System;
using RimAI.Core.Source.Modules.LLM;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class LLM_JsonModeDemoButton
	{
		public static void Draw(Listing_Standard listing, ILLMService llm)
		{
			if (listing.ButtonText("[P2] JSON Mode (non-stream)"))
			{
				_ = System.Threading.Tasks.Task.Run(async () => await RunAsync(llm));
			}
		}

		private static async System.Threading.Tasks.Task RunAsync(ILLMService llm)
		{
			var conv = "debug-json-" + DateTime.UtcNow.Ticks;
			var resp = await llm.GetResponseAsync(conv,
				"You are a JSON-only assistant. Always return a valid JSON object.",
				"返回一个对象，包含字段 name: 'RimAI', version: '5.0'.",
				jsonMode: true);
			if (resp.IsSuccess)
			{
				Log.Message($"[RimAI.Core][P2.LLM] json ok: {resp.Value?.Message?.Content}");
			}
			else
			{
				Log.Error($"[RimAI.Core][P2.LLM] json failed: {resp.Error}");
			}
		}
	}
}


