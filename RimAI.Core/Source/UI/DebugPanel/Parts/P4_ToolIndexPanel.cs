using System;
using System.Linq;
using System.Threading;
using RimAI.Core.Source.Modules.Tooling;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P4_ToolIndexPanel
	{
		private static string _topkInput = "殖民地概况";
		private static int _topk = 5;
		private static float _minScore = 0f;

		public static void Draw(Listing_Standard listing, IToolRegistryService tooling)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P4] Tooling & Index");
			Text.Font = GameFont.Small;
			listing.GapLine();

			var isReady = tooling.IsIndexReady();
			listing.Label($"Index Ready: {isReady}");
			var fp = tooling.GetIndexFingerprint();
			if (fp != null)
			{
				listing.Label($"Fingerprint: {fp.Provider}/{fp.Model} dim={fp.Dimension}");
			}

			if (listing.ButtonText("Rebuild Index"))
			{
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try { await tooling.RebuildIndexAsync(default); Verse.Log.Message("[RimAI.Core][P4] Rebuild requested"); }
					catch (Exception ex) { Verse.Log.Error($"[RimAI.Core][P4] rebuild failed: {ex.Message}"); }
				});
			}

			if (listing.ButtonText("Ensure Built"))
			{
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try { await tooling.EnsureIndexBuiltAsync(default); Verse.Log.Message("[RimAI.Core][P4] EnsureBuilt requested"); }
					catch (Exception ex) { Verse.Log.Error($"[RimAI.Core][P4] ensure failed: {ex.Message}"); }
				});
			}

			// TopK 试算
			listing.Label("TopK query text:");
			_topkInput = Widgets.TextField(listing.GetRect(28f), _topkInput);
			listing.Label("TopK:");
			var topkRect = listing.GetRect(28f);
			var topkStr = _topk.ToString();
			var newTopkStr = Widgets.TextField(topkRect, topkStr);
			if (int.TryParse(newTopkStr, out var newTopk) && newTopk > 0) _topk = newTopk;
			listing.Label("MinScore:");
			var msRect = listing.GetRect(28f);
			var str = _minScore.ToString();
			var newStr = Widgets.TextField(msRect, str);
			if (float.TryParse(newStr, out var f)) _minScore = f;

			if (listing.ButtonText("Run TopK"))
			{
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try
					{
						var r = await tooling.GetNarrowTopKToolCallSchemaAsync(_topkInput, _topk, _minScore, null, default);
						var scores = string.Join(", ", r.Scores.Select(s => $"{s.ToolName}:{s.Score:F3}"));
						Verse.Log.Message($"[RimAI.Core][P4] TopK ok => {scores}");
					}
					catch (Exception ex)
					{
						Verse.Log.Error($"[RimAI.Core][P4] TopK failed: {ex.Message}");
					}
				});
			}
		}
	}
}


