using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Orchestration;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P5_OrchestrationPanel
	{
		private static string _input = "帮我获取殖民地的状态";
		private static int _topk = 5;
		private static string _profile = "Fast";

		public static void Draw(Listing_Standard listing, IOrchestrationService orch)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P5] Orchestration-Min");
			Text.Font = GameFont.Small;
			listing.GapLine();

			listing.Label("User Input:");
			_input = listing.TextEntry(_input);
			listing.Label("ExecutionProfile (Fast/Deep):");
			_profile = listing.TextEntry(_profile);
			listing.Label("TopK (NarrowTopK):");
			int.TryParse(listing.TextEntry(_topk.ToString()), out _topk);

			if (listing.ButtonText("Run Classic/Fast"))
			{
				_ = Task.Run(async () => await RunAsync(orch, OrchestrationMode.Classic));
			}
			if (listing.ButtonText("Run NarrowTopK/Fast"))
			{
				_ = Task.Run(async () => await RunAsync(orch, OrchestrationMode.NarrowTopK));
			}
		}

		private static async Task RunAsync(IOrchestrationService orch, OrchestrationMode mode)
		{
			try
			{
				var opts = new ToolOrchestrationOptions
				{
					Mode = mode,
					Profile = string.Equals(_profile, "Deep", StringComparison.OrdinalIgnoreCase) ? ExecutionProfile.Deep : ExecutionProfile.Fast,
					MaxCalls = 1,
					NarrowTopK = _topk,
					MinScoreThreshold = 0.0
				};
				var res = await orch.ExecuteAsync(_input, new List<string> { "agent:stage" }, opts, CancellationToken.None);
				if (res.IsSuccess)
				{
					Log.Message($"[RimAI.Core][P5] success mode={res.Mode} calls={res.DecidedCalls?.Count ?? 0} executed={res.Executions?.Count ?? 0} ms={res.TotalLatencyMs}");
				}
				else
				{
					Log.Warning($"[RimAI.Core][P5] failed mode={mode} error={res.Error}");
				}
			}
			catch (Exception ex)
			{
				Log.Error($"[RimAI.Core][P5] exception: {ex.Message}");
			}
		}
	}
}


