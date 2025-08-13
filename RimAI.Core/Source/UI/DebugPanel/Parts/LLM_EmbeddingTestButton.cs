using System;
using RimAI.Core.Source.Modules.LLM;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class LLM_EmbeddingTestButton
	{
		public static void Draw(Listing_Standard listing, ILLMService llm)
		{
			if (listing.ButtonText("[P2] Embedding Test"))
			{
				_ = RunAsync(llm);
			}
		}

		private static async System.Threading.Tasks.Task RunAsync(ILLMService llm)
		{
			var text = "RimAI.Core provides AI-driven experiences for RimWorld.";
            var r = await llm.GetEmbeddingsAsync(text);
			if (r.IsSuccess)
			{
                var vectors = r.Value?.Data?.Count ?? 0;
                var dims = 0;
                var top1norm = 0.0;
                if (vectors > 0 && r.Value.Data[0].Embedding != null)
                {
                    dims = r.Value.Data[0].Embedding.Count;
                    foreach (var v in r.Value.Data[0].Embedding) top1norm += v * v;
                    top1norm = Math.Sqrt(top1norm);
                }
                Log.Message($"[RimAI.Core][P2.LLM] embed dims={dims} vectors={vectors} top1norm={top1norm:F3}");
			}
			else
			{
				Log.Error($"[RimAI.Core][P2.LLM] embed failed: {r.Error}");
			}
		}
	}
}


