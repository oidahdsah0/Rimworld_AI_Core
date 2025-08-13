using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P3_WorldDataPanel
	{
		public static void Draw(Listing_Standard listing, IWorldDataService world)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P3] WorldData");
			Text.Font = GameFont.Small;
			listing.GapLine();

			if (listing.ButtonText("GetPlayerName"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var sw = Stopwatch.StartNew();
						var name = await world.GetPlayerNameAsync(CancellationToken.None);
						sw.Stop();
						Log.Message($"[RimAI.Core][P3] PlayerName='{name}' elapsed={sw.Elapsed.TotalMilliseconds:F2} ms");
					}
					catch (Exception ex)
					{
						Log.Warning($"[RimAI.Core][P3] GetPlayerName failed: {ex.Message}");
					}
				});
			}
		}
	}
}


