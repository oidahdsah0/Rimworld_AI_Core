using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerInspectionHistoryTab
	{
		private static string BuildInspectionConvKey(string entityId)
		{
			try
			{
				int? id = TryParseThingId(entityId);
				var list = new List<string> { "agent:server_inspection", id.HasValue ? ($"server_inspection:{id.Value}") : ($"server_inspection:{(entityId ?? "unknown")}" ) };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_inspection|server_inspection:unknown"; }
		}

		private static int? TryParseThingId(string entityId)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(entityId)) return null;
				var s = entityId.Trim();
				if (int.TryParse(s, out var pure)) return pure;
				var lastIdx = s.LastIndexOf(':');
				if (lastIdx >= 0 && lastIdx + 1 < s.Length)
				{
					var tail = s.Substring(lastIdx + 1);
					if (int.TryParse(tail, out var id2)) return id2;
				}
			}
			catch { }
			return null;
		}

		private sealed class State
		{
			public Vector2 Scroll;
			public bool Clearing;
			public string LoadedConvKey;
			public List<RimAI.Core.Source.Modules.History.Models.HistoryEntry> Entries;
		}

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, State> _stateByEntity = new System.Collections.Concurrent.ConcurrentDictionary<string, State>(StringComparer.OrdinalIgnoreCase);

		public static void Draw(Rect inRect, string entityId, IHistoryService history)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			var state = _stateByEntity.GetOrAdd(entityId, _ => new State());
			var convKey = BuildInspectionConvKey(entityId);
			if (!string.Equals(state.LoadedConvKey, convKey, StringComparison.Ordinal))
			{
				state.LoadedConvKey = convKey;
				state.Entries = null;
			}

			var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
			var btnRect = new Rect(inRect.xMax - 140f, inRect.y + 2f, 130f, 26f);
			Text.Font = GameFont.Medium;
			Widgets.Label(headerRect, "RimAI.SCW.InspectionHistory.Header".Translate());
			var prev = GUI.enabled; GUI.enabled = !state.Clearing;
			if (Widgets.ButtonText(btnRect, "RimAI.SCW.InspectionHistory.Clear".Translate()))
			{
				state.Clearing = true;
				_ = Task.Run(async () =>
				{
					try { await history.ClearThreadAsync(convKey).ConfigureAwait(false); }
					catch { }
					finally { state.Clearing = false; state.Entries = null; }
				});
			}
			GUI.enabled = prev;

			var contentRect = new Rect(inRect.x, headerRect.yMax + 6f, inRect.width, inRect.height - 36f);
			EnsureLoaded(history, state, convKey);
			DrawEntries(contentRect, state);
		}

		private static void EnsureLoaded(IHistoryService history, State state, string convKey)
		{
			if (state.Entries != null) return;
			try { state.Entries = new List<RimAI.Core.Source.Modules.History.Models.HistoryEntry>(history.GetAllEntriesAsync(convKey).GetAwaiter().GetResult()); }
			catch { state.Entries = new List<RimAI.Core.Source.Modules.History.Models.HistoryEntry>(); }
		}

		private static void DrawEntries(Rect rect, State state)
		{
			var entries = state.Entries ?? new List<RimAI.Core.Source.Modules.History.Models.HistoryEntry>();
			float totalH = 4f;
			float contentW = rect.width - 16f;
			for (int i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				var txt = e?.Content ?? string.Empty;
				totalH += Mathf.Max(24f, Text.CalcHeight(txt, contentW)) + 8f;
			}
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref state.Scroll, viewRect);
			float y = 4f;
			for (int i = 0; i < entries.Count; i++)
			{
				var e = entries[i];
				var txt = e?.Content ?? string.Empty;
				float h = Mathf.Max(24f, Text.CalcHeight(txt, contentW));
				var row = new Rect(0f, y, viewRect.width, h + 6f);
				Widgets.DrawHighlightIfMouseover(row);
				Widgets.Label(new Rect(row.x + 6f, row.y + 3f, contentW, h), txt);
				y += h + 8f;
			}
			Widgets.EndScrollView();
		}
	}
}


