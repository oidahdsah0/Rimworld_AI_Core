using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private bool _loading;
        private string _error;
        private CancellationTokenSource _cts;

		public void Draw(Rect rect, string filterServerEntityId)
		{
			if ((_entries == null || !string.Equals(_lastServerId, filterServerEntityId, StringComparison.Ordinal)) && !_loading)
			{
				KickoffReload(filterServerEntityId);
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
			if (_loading)
			{
				Widgets.Label(new Rect(4f, y, contentW, 24f), "Loading...");
			}
			else if (!string.IsNullOrEmpty(_error))
			{
				Widgets.Label(new Rect(4f, y, contentW, 24f), _error);
			}
			else if (_entries != null)
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

		private void KickoffReload(string filterServerEntityId)
		{
			_loading = true; _error = null; _entries = null; _lastServerId = filterServerEntityId;
			try { _cts?.Cancel(); } catch { }
			_cts = new CancellationTokenSource(); var ct = _cts.Token;
			_ = Task.Run(async () =>
			{
				try
				{
					var history = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IHistoryService>();
					string stageCk = "agent:stage";
					var thread = await history.GetThreadAsync(stageCk, 1, 200).ConfigureAwait(false);
					var list = new List<HistoryEntry>();
					if (thread?.Entries != null)
					{
						foreach (var e in thread.Entries)
						{
							if (ct.IsCancellationRequested) return;
							if (e == null || e.Deleted) continue;
							if (!string.IsNullOrWhiteSpace(filterServerEntityId))
							{
								if ((e.Content ?? string.Empty).IndexOf(filterServerEntityId, StringComparison.OrdinalIgnoreCase) < 0) continue;
							}
							list.Add(e);
						}
					}
					_entries = list;
				}
				catch (OperationCanceledException) { }
				catch (Exception ex) { _error = ex.Message; _entries = new List<HistoryEntry>(); }
				finally { _loading = false; }
			});
		}
	}
}


