using System;
using RimAI.Core.Source.Modules.Tooling;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P4_ToolRunner
	{
		public static void Draw(Listing_Standard listing, IToolRegistryService tooling)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P4] Tool Runner");
			Text.Font = GameFont.Small;
			if (listing.ButtonText("Run get_colony_status"))
			{
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try
					{
						var r = await tooling.ExecuteToolAsync("get_colony_status", new System.Collections.Generic.Dictionary<string, object>());
						Log.Message($"[RimAI.Core][P4] Tool result: {Newtonsoft.Json.JsonConvert.SerializeObject(r)}");
					}
					catch (Exception ex)
					{
						Log.Error($"[RimAI.Core][P4] Tool run failed: {ex.Message}");
					}
				});
			}
		}
	}
}



