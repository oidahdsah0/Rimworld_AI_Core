using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.History.Relations;
using RimAI.Core.Source.Modules.History.Models;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel.Parts
{
	internal static class P8_HistoryPanel
	{
		private static string _convKey = string.Empty;
		private static string _userText = string.Empty;
		private static string _aiText = string.Empty;
		private static string _participantsCsv = string.Empty;
		private static Vector2 _histScroll = Vector2.zero;
		private static Vector2 _recapScroll = Vector2.zero;
		private static Vector2 _relScroll = Vector2.zero;
		private static string _relInput = string.Empty;
		private static int _page = 1;
		private static int _pageSize = 20;

		public static void Draw(Listing_Standard listing, IHistoryService history, IRecapService recap, IRelationsService relations)
		{
            Text.Font = GameFont.Medium;
            listing.Label(Keys.P8 + " History");
            Text.Font = GameFont.Small;
            listing.GapLine();

            listing.Label("convKey:");
            _convKey = Widgets.TextField(listing.GetRect(24f), _convKey ?? string.Empty);
            listing.Label("Participants (comma-separated):");
            _participantsCsv = Widgets.TextField(listing.GetRect(24f), _participantsCsv ?? string.Empty);
            listing.Label("User Text:");
            _userText = Widgets.TextField(listing.GetRect(24f), _userText ?? string.Empty);
            listing.Label("AI Final Text:");
            _aiText = Widgets.TextField(listing.GetRect(24f), _aiText ?? string.Empty);
            if (listing.ButtonText("Append Pair"))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var parts = (_participantsCsv ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
                        if (parts.Count > 0) await history.UpsertParticipantsAsync(_convKey, parts, default);
                        await history.AppendPairAsync(_convKey, _userText, _aiText, default);
                        Log.Message(Keys.P8 + " appended pair");
                    }
                    catch (Exception ex) { Log.Error(Keys.P8 + " append failed: " + ex.Message); }
                });
            }

            listing.GapLine();
            listing.Label("History Thread (paged):");
            var outRect = listing.GetRect(160f);
            Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.07f));
            var th = history.GetThreadAsync(_convKey, _page, _pageSize).GetAwaiter().GetResult();
            var partsNow = string.Join(", ", history.GetParticipantsOrEmpty(_convKey));
            var content = (string.IsNullOrEmpty(partsNow) ? string.Empty : ("Participants: " + partsNow + "\n")) + (th?.Entries == null ? string.Empty : string.Join("\n", th.Entries.Select(e => $"{e.Timestamp:HH:mm:ss} [{e.Role}] {(e.TurnOrdinal?.ToString() ?? "-")}: {e.Content}")));
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(160f, Text.CalcHeight(content, outRect.width - 16f) + 8f));
            Widgets.BeginScrollView(outRect, ref _histScroll, viewRect);
            Widgets.Label(viewRect, content);
            Widgets.EndScrollView();
            listing.Label($"page={_page}/{Mathf.Max(1, Mathf.CeilToInt((th?.TotalEntries ?? 0) / (float)Math.Max(1,_pageSize)))} total={th?.TotalEntries ?? 0}");
            if (listing.ButtonText("Prev Page")) _page = Math.Max(1, _page - 1);
            if (listing.ButtonText("Next Page")) _page += 1;

            listing.GapLine();
            listing.Label("Recaps:");
            var recaps = recap.GetRecaps(_convKey);
            var recapText = string.Join("\n---\n", recaps.Select(r => $"[{r.Mode}] ({r.FromTurnExclusive},{r.ToTurnInclusive}] len={r.Text?.Length ?? 0}\n{r.Text}"));
            var outRect2 = listing.GetRect(140f);
            Widgets.DrawBoxSolid(outRect2, new Color(0f, 0f, 0f, 0.06f));
            var viewRect2 = new Rect(0f, 0f, outRect2.width - 16f, Mathf.Max(140f, Text.CalcHeight(recapText, outRect2.width - 16f) + 8f));
            Widgets.BeginScrollView(outRect2, ref _recapScroll, viewRect2);
            Widgets.Label(viewRect2, recapText);
            Widgets.EndScrollView();
            if (listing.ButtonText("Force Rebuild Recaps"))
            {
                _ = Task.Run(async () => { await recap.ForceRebuildAsync(_convKey); Log.Message(Keys.P8 + " force rebuild queued"); });
            }
            if (listing.ButtonText("Rebuild Stale Recaps"))
            {
                _ = Task.Run(async () => { await recap.RebuildStaleAsync(_convKey); Log.Message(Keys.P8 + " stale rebuild queued"); });
            }

            listing.GapLine();
            listing.Label("Relations (comma-separated participantIds):");
            _relInput = Widgets.TextField(listing.GetRect(24f), _relInput ?? string.Empty);
            var ids = (_relInput ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
            var sup = relations.ListSupersetsAsync(ids, 1, 20).GetAwaiter().GetResult();
            var sub = relations.ListSubsetsAsync(ids, 1, 20).GetAwaiter().GetResult();
            var relText = "Supersets:\n" + string.Join(", ", sup.ConvKeys) + "\nSubsets:\n" + string.Join(", ", sub.ConvKeys);
            var outRect3 = listing.GetRect(120f);
            Widgets.DrawBoxSolid(outRect3, new Color(0f, 0f, 0f, 0.06f));
            var viewRect3 = new Rect(0f, 0f, outRect3.width - 16f, Mathf.Max(120f, Text.CalcHeight(relText, outRect3.width - 16f) + 8f));
            Widgets.BeginScrollView(outRect3, ref _relScroll, viewRect3);
            Widgets.Label(viewRect3, relText);
            Widgets.EndScrollView();

            listing.GapLine();
        }
	}
}


