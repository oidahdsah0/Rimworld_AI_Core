using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Modules.Persistence.Diagnostics;
using RimAI.Core.Source.Modules.Persistence.Snapshots;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P6_PersistencePanel
	{
		private static string _importJson = string.Empty;

		public static void Draw(Listing_Standard listing, IPersistenceService persistence)
		{
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P6] Persistence");
			Text.Font = GameFont.Small;
			listing.GapLine();

			var stats = persistence.GetLastStats();
			if (stats != null)
			{
				listing.Label($"Last: op={stats.Operation}, nodes={stats.Nodes}, elapsed={stats.ElapsedMs}ms");
				foreach (var d in stats.Details.Take(8))
				{
					listing.Label($"- {d.Node}: ok={d.Ok}, entries={d.Entries}, bytes≈{d.BytesApprox}, elapsed={d.ElapsedMs}ms{(string.IsNullOrEmpty(d.Error) ? string.Empty : ", err=" + d.Error)}");
				}
			}

			if (listing.ButtonText("Export JSON (to Config/RimAI/Snapshots)"))
			{
				_ = Task.Run(async () =>
				{
					try
					{
						var json = persistence.ExportAllToJson();
						var fileName = $"Snapshots/export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
						await persistence.WriteTextUnderConfigAsync($"Config/RimAI/{fileName}", json, default);
						Log.Message($"[RimAI.Core][P6] Exported snapshot => {fileName}");
					}
					catch (Exception ex)
					{
						Log.Error($"[RimAI.Core][P6] Export failed: {ex.Message}");
					}
				});
			}

			listing.Label("Import JSON (paste below):");
			var box = listing.GetRect(80f);
			_importJson = Widgets.TextArea(box, _importJson);
			if (listing.ButtonText("Import Pasted JSON"))
			{
				try
				{
					persistence.ImportAllFromJson(_importJson);
					Log.Message("[RimAI.Core][P6] Import buffered. Will be saved on next Save.");
				}
				catch (Exception ex)
				{
					Log.Error($"[RimAI.Core][P6] Import failed: {ex.Message}");
				}
			}

			if (listing.ButtonText("Rebuild History Indexes (in-memory)"))
			{
				try
				{
					var snap = persistence.GetLastSnapshotForDebug() ?? new PersistenceSnapshot();
					RebuildHistoryIndexes(snap);
					persistence.ReplaceLastSnapshotForDebug(snap);
					Log.Message("[RimAI.Core][P6] History indexes rebuilt (buffered, will persist on next Save)");
				}
				catch (Exception ex)
				{
					Log.Error($"[RimAI.Core][P6] Rebuild failed: {ex.Message}");
				}
			}
		}

		private static void RebuildHistoryIndexes(PersistenceSnapshot snap)
		{
			snap.History.ConvKeyIndex.Clear();
			snap.History.ParticipantIndex.Clear();
			foreach (var kv in snap.History.Conversations)
			{
				var convId = kv.Key;
				var cr = kv.Value;
				if (cr?.ParticipantIds == null || cr.ParticipantIds.Count == 0) continue;
				var convKey = string.Join("|", cr.ParticipantIds.OrderBy(x => x));
				if (!snap.History.ConvKeyIndex.TryGetValue(convKey, out var list1))
				{
					list1 = new System.Collections.Generic.List<string>();
					snap.History.ConvKeyIndex[convKey] = list1;
				}
				if (!list1.Contains(convId)) list1.Add(convId);
				foreach (var pid in cr.ParticipantIds)
				{
					if (!snap.History.ParticipantIndex.TryGetValue(pid, out var list2))
					{
						list2 = new System.Collections.Generic.List<string>();
						snap.History.ParticipantIndex[pid] = list2;
					}
					if (!list2.Contains(convId)) list2.Add(convId);
				}
			}
		}
	}
}


