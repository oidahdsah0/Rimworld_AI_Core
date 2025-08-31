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
		private static string BuildInspectionConvKeyFromUiConvKey(string uiConvKey, IHistoryService history)
		{
			try
			{
				// 从当前 UI 会话键中解析参与者，提取 server:<id>
				int? thingId = null;
				try
				{
					var parts = history?.GetParticipantsOrEmpty(uiConvKey) ?? new List<string>();
					foreach (var p in parts)
					{
						if (string.IsNullOrWhiteSpace(p)) continue;
						if (p.StartsWith("server:", StringComparison.OrdinalIgnoreCase))
						{
							var tail = p.Substring("server:".Length);
							if (int.TryParse(tail, out var idv)) { thingId = idv; break; }
						}
					}
				}
				catch { }
				// 退路：直接从会话键文本中解析 server:<id>
				if (!thingId.HasValue && !string.IsNullOrWhiteSpace(uiConvKey))
				{
					try
					{
						var tokens = uiConvKey.Split('|');
						foreach (var t in tokens)
						{
							var s = t?.Trim(); if (string.IsNullOrEmpty(s)) continue;
							if (s.StartsWith("server:", StringComparison.OrdinalIgnoreCase))
							{
								var tail = s.Substring("server:".Length);
								if (int.TryParse(tail, out var id2)) { thingId = id2; break; }
							}
						}
					}
					catch { }
				}
				var list = new List<string> { "agent:server_inspection", thingId.HasValue ? ($"server_inspection:{thingId.Value}") : "server_inspection:unknown" };
				list.Sort(StringComparer.Ordinal);
				return string.Join("|", list);
			}
			catch { return "agent:server_inspection|server_inspection:unknown"; }
		}

		private sealed class State
		{
			public Vector2 Scroll;
			public bool Clearing;
			public string LoadedConvKey;
			public List<RimAI.Core.Source.Modules.History.Models.HistoryEntry> Entries;
			public double NextRefreshRealtime; // 节流自动刷新
		}

		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, State> _stateByEntity = new System.Collections.Concurrent.ConcurrentDictionary<string, State>(StringComparer.OrdinalIgnoreCase);

		public static void Draw(Rect inRect, string uiServerConvKey, IHistoryService history)
		{
			if (string.IsNullOrWhiteSpace(uiServerConvKey)) return;
			// 以 UI 会话键为分组键缓存状态
			var state = _stateByEntity.GetOrAdd(uiServerConvKey ?? string.Empty, _ => new State());
			var convKey = BuildInspectionConvKeyFromUiConvKey(uiServerConvKey, history);
			if (!string.Equals(state.LoadedConvKey, convKey, StringComparison.Ordinal))
			{
				state.LoadedConvKey = convKey;
				state.Entries = null;
			}

			var headerRect = new Rect(inRect.x, inRect.y, inRect.width, 30f);
			var btnRect = new Rect(inRect.xMax - 140f, inRect.y + 2f, 130f, 26f);
			// 标题使用 Medium 字体
			var prevFont = Text.Font;
			Text.Font = GameFont.Medium;
			Widgets.Label(headerRect, "RimAI.SCW.InspectionHistory.Header".Translate());
			// 其余内容（按钮与列表）统一使用 Small 字体
			Text.Font = GameFont.Small;
			// ...
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
			// 恢复外部字体状态，避免影响其他 UI
			Text.Font = prevFont;
		}

	private static void EnsureLoaded(IHistoryService history, State state, string convKey)
		{
			// 自动刷新：首次或到期都重新拉取，避免“触发后不出现”的观感问题
			var now = Time.realtimeSinceStartup;
			if (state.Entries != null && now < state.NextRefreshRealtime) return;
			try { state.Entries = new List<RimAI.Core.Source.Modules.History.Models.HistoryEntry>(history.GetAllEntriesAsync(convKey).GetAwaiter().GetResult()); state.NextRefreshRealtime = now + 1.0f; }
			catch { state.Entries = new List<RimAI.Core.Source.Modules.History.Models.HistoryEntry>(); }
		}

		private static void DrawEntries(Rect rect, State state)
		{
			var prevFont = Text.Font;
			Text.Font = GameFont.Small; // 记录内容统一使用 Small 字体
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
			Text.Font = prevFont;
		}
	}
}


