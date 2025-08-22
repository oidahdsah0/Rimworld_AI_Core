using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal sealed class InterServerCommsPanel
	{
		private Vector2 _scroll = Vector2.zero;
		private List<HistoryEntry> _entries;
		private string _lastServerId;

		public void Draw(Rect rect, string filterServerEntityId)
		{
			if (_entries == null || !string.Equals(_lastServerId, filterServerEntityId, StringComparison.Ordinal))
			{
				Reload(filterServerEntityId);
			}
			float totalH = 4f;
			float contentW = rect.width - 16f;
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var e = _entries[i];
					string label = $"[{e.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}] {e.Content}";
					totalH += Mathf.Max(24f, Text.CalcHeight(label, contentW)) + 6f;
				}
			}
			var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref _scroll, view);
			float y = 4f;
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var e = _entries[i];
					string label = $"[{e.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm}] {e.Content}";
					float rowH = Mathf.Max(24f, Text.CalcHeight(label, contentW)) + 6f;
					Widgets.Label(new Rect(4f, y, contentW, rowH), label);
					y += rowH + 2f;
				}
			}
			Widgets.EndScrollView();
		}

		private void Reload(string filterServerEntityId)
		{
			_entries = new List<HistoryEntry>();
			_lastServerId = filterServerEntityId;
			try
			{
				var history = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IHistoryService>();
				string stageCk = "agent:stage";
				var thread = history.GetThreadAsync(stageCk, 1, 200).GetAwaiter().GetResult();
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						if (!string.IsNullOrWhiteSpace(filterServerEntityId))
						{
							// 简化：按内容包含 server id 过滤（Stage 写入可附带 server 元信息后再增强）
							if ((e.Content ?? string.Empty).IndexOf(filterServerEntityId, StringComparison.OrdinalIgnoreCase) < 0) continue;
						}
						_entries.Add(e);
					}
				}
			}
			catch { _entries = new List<HistoryEntry>(); }
		}
	}
}


