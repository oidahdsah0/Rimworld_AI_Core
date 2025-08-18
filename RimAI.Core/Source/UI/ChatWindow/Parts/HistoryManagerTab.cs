using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class HistoryManagerTabView
	{
		private enum HistorySubTab { Thread, Recaps, Related }
		private HistorySubTab _subTab = HistorySubTab.Thread;
		private Vector2 _scrollThread = Vector2.zero;
		private Vector2 _scrollRecaps = Vector2.zero;
		private Vector2 _scrollRelated = Vector2.zero;
		private bool _recapGenerating = false;

		private sealed class HistoryEntryVM { public string Id; public EntryRole Role; public string Content; public DateTime TimestampUtc; public bool IsEditing; public string EditText; }
		private List<HistoryEntryVM> _entries;
		private sealed class RecapVM { public string Id; public string Text; public bool IsEditing; public string EditText; public string Range; public DateTime UpdatedAtUtc; }
		private List<RecapVM> _recaps;
		private List<string> _relatedConvs;
		private int _relatedSelectedIdx = -1;
		// Recap 实时刷新订阅
		private IRecapService _recapHooked;
		private string _recapHookedConvKey;
		private bool _recapDirty;
		private Action<string, string> _recapHandler;
		// 显示名缓存：convKey → (playerTitle, pawnName)
		private readonly System.Collections.Generic.Dictionary<string, string> _convUserName = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
		private readonly System.Collections.Generic.Dictionary<string, string> _convPawnName = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
		private readonly System.Collections.Generic.HashSet<string> _nameResolving = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

		public void Draw(Rect inRect, RimAI.Core.Source.UI.ChatWindow.ChatConversationState state, IHistoryService history, IRecapService recap, Action<string> switchToConvKey)
		{
			float tabsH = 28f; float sp = 6f; float btnW = 110f;
			var rTabs = new Rect(inRect.x, inRect.y, inRect.width, tabsH);
			if (Widgets.ButtonText(new Rect(rTabs.x, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Thread".Translate())) _subTab = HistorySubTab.Thread;
			if (Widgets.ButtonText(new Rect(rTabs.x + btnW + sp, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Recaps".Translate())) _subTab = HistorySubTab.Recaps;
			if (Widgets.ButtonText(new Rect(rTabs.x + (btnW + sp) * 2, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Related".Translate())) _subTab = HistorySubTab.Related;
			var contentRect = new Rect(inRect.x, rTabs.yMax + 8f, inRect.width, inRect.height - tabsH - 12f);

			switch (_subTab)
			{
				case HistorySubTab.Thread:
					EnsureHistoryLoaded(history, state.ConvKey);
					DrawThread(contentRect, history, state.ConvKey);
					break;
				case HistorySubTab.Recaps:
					EnsureRecapsLoaded(recap, state.ConvKey);
					DrawRecaps(contentRect, recap, state.ConvKey);
					break;
				case HistorySubTab.Related:
					EnsureRelatedLoaded(history, state.ParticipantIds);
					DrawRelated(contentRect, switchToConvKey);
					break;
			}
		}

		private void EnsureHistoryLoaded(IHistoryService history, string convKey)
		{
			if (_entries != null) return;
			try
			{
				var thread = history.GetThreadAsync(convKey, 1, 200).GetAwaiter().GetResult();
				_entries = new List<HistoryEntryVM>();
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						_entries.Add(new HistoryEntryVM { Id = e.Id, Role = e.Role, Content = e.Content, TimestampUtc = e.Timestamp, IsEditing = false, EditText = e.Content });
					}
				}
			}
			catch { _entries = new List<HistoryEntryVM>(); }
		}

		private void ReloadHistory(IHistoryService history, string convKey)
		{
			_entries = null; EnsureHistoryLoaded(history, convKey);
		}

		public void ForceReloadHistory(IHistoryService history, string convKey)
		{
			ReloadHistory(history, convKey);
		}

		private void DrawThread(Rect rect, IHistoryService history, string convKey)
		{
			// 先计算总高度以启用滚动
			float totalH = 4f;
			float actionsWForMeasure = 200f;
			float contentWForMeasure = (rect.width - 16f) - actionsWForMeasure - 16f;
			var names = GetOrBeginResolveNames(history, convKey);
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var it = _entries[i];
					var measureRect = new Rect(0f, 0f, contentWForMeasure, 99999f);
					string senderNameMeasure = it.Role == EntryRole.User ? names.userName : names.pawnName;
					string labelForMeasure = (senderNameMeasure ?? string.Empty) + "：" + (it.Content ?? string.Empty);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(labelForMeasure, measureRect.width));
					float rowH = contentH + 12f;
					totalH += rowH + 6f;
				}
			}
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref _scrollThread, viewRect);
			float y = 4f;
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var it = _entries[i];
					float actionsW = 200f;
					float contentW = viewRect.width - actionsW - 16f;
					var contentMeasureRect = new Rect(0f, 0f, contentW, 99999f);
					string senderName = it.Role == EntryRole.User ? names.userName : names.pawnName;
					string label = (senderName ?? string.Empty) + "：" + (it.Content ?? string.Empty);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, contentMeasureRect.width)) : Mathf.Max(24f, Text.CalcHeight(label, contentMeasureRect.width));
					float rowH = contentH + 12f;
					var row = new Rect(0f, y, viewRect.width, rowH);
					Widgets.DrawHighlightIfMouseover(row);
					var contentRect = new Rect(row.x + 6f, row.y + 6f, contentW, contentH);
					var actionsRect = new Rect(row.xMax - (actionsW + 10f), row.y + 8f, actionsW, 28f);
					label = (senderName ?? string.Empty) + "：" + (it.Content ?? string.Empty);
					if (!it.IsEditing)
					{
						Widgets.Label(contentRect, label);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Content; }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { _ = DeleteEntryAsync(history, convKey, it.Id); }
					}
					else
					{
						it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { _ = SaveEntryAsync(history, convKey, it.Id, it.EditText); }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Content; }
					}
					y += rowH + 6f;
				}
			}
			Widgets.EndScrollView();
		}

		private async Task SaveEntryAsync(IHistoryService history, string convKey, string entryId, string text)
		{
			try { var ok = await history.EditEntryAsync(convKey, entryId, text); if (!ok) Verse.Log.Warning("[RimAI.Core][P10] EditEntryAsync failed"); }
			catch (Exception ex) { Verse.Log.Warning($"[RimAI.Core][P10] EditEntryAsync error: {ex.Message}"); }
			finally { ReloadHistory(history, convKey); }
		}

		private async Task DeleteEntryAsync(IHistoryService history, string convKey, string entryId)
		{
			try { var ok = await history.DeleteEntryAsync(convKey, entryId); if (!ok) Verse.Log.Warning("[RimAI.Core][P10] DeleteEntryAsync failed"); }
			catch (Exception ex) { Verse.Log.Warning($"[RimAI.Core][P10] DeleteEntryAsync error: {ex.Message}"); }
			finally { ReloadHistory(history, convKey); }
		}

		private void EnsureRecapsLoaded(IRecapService recap, string convKey)
		{
			if (_recaps != null) return;
			try
			{
				EnsureRecapEventHooked(recap, convKey);
				var items = recap.GetRecaps(convKey) ?? Array.Empty<RecapItem>();
				_recaps = new List<RecapVM>();
				foreach (var r in items)
				{
					string range = $"{r.FromTurnExclusive + 1}..{r.ToTurnInclusive}";
					_recaps.Add(new RecapVM { Id = r.Id, Text = r.Text, IsEditing = false, EditText = r.Text, Range = range, UpdatedAtUtc = r.UpdatedAt });
				}
			}
			catch { _recaps = new List<RecapVM>(); }
		}

		private void ReloadRecaps(IRecapService recap, string convKey)
		{
			_recaps = null; EnsureRecapsLoaded(recap, convKey);
		}

		public void ForceReloadRecaps(IRecapService recap, string convKey)
		{
			ReloadRecaps(recap, convKey);
		}

		private void DrawRecaps(Rect rect, IRecapService recap, string convKey)
		{
			// 监听服务端更新事件，并在下一帧刷新
			EnsureRecapEventHooked(recap, convKey);
			if (_recapDirty)
			{
				ReloadRecaps(recap, convKey);
				_recapDirty = false;
			}
			// 动态高度（富文本跟随高度）
			float totalH = 4f;
			float actionsW = 200f;
			float contentW = (rect.width - 16f) - actionsW - 16f;
			if (_recaps != null)
			{
				// 生成按钮行
				totalH += 34f;
				for (int i = 0; i < _recaps.Count; i++)
				{
					var it = _recaps[i];
					var measureRect = new Rect(0f, 0f, contentW, 99999f);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
					float headerH = 28f;
					float headerPad = 2f;
					float rowH = headerH + headerPad + contentH + 12f;
					totalH += rowH + 6f;
				}
			}
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Math.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref _scrollRecaps, viewRect);
			float y = 4f;
			if (_recaps != null)
			{
				// 手动触发前情提要按钮
				if (!_recapGenerating && Widgets.ButtonText(new Rect(viewRect.x, y, 160f, 28f), "RimAI.ChatUI.Recap.Generate".Translate()))
				{
					_recapGenerating = true;
					_ = System.Threading.Tasks.Task.Run(async () =>
					{
						try { await recap.GenerateManualAsync(convKey); }
						catch (Exception ex) { try { Verse.Log.Warning($"[RimAI.Core][UI] Manual recap failed conv={convKey}: {ex.Message}"); } catch { } }
						finally { _recapGenerating = false; ReloadRecaps(recap, convKey); }
					});
				}
				if (_recapGenerating)
				{
					Widgets.Label(new Rect(viewRect.x + 170f, y + 4f, 200f, 22f), "RimAI.ChatUI.Recap.Generating".Translate());
				}
				y += 34f;
				for (int i = 0; i < _recaps.Count; i++)
				{
					var it = _recaps[i];
					var measureRect = new Rect(0f, 0f, contentW, 99999f);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
					float headerH = 28f;
					float headerPad = 2f;
					float rowH = headerH + headerPad + contentH + 12f;
					var row = new Rect(0f, y, viewRect.width, rowH);
					Widgets.DrawHighlightIfMouseover(row);
					var headerRect = new Rect(row.x + 6f, row.y + 6f, contentW, headerH);
					var contentRect = new Rect(row.x + 6f, headerRect.yMax + headerPad, contentW, contentH);
					var actionsRect = new Rect(row.xMax - 210f, row.y + 8f, 200f, row.height - 16f);
					// 标题行（范围+时间）
					Widgets.Label(headerRect, $"[{it.Range}] {it.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
					if (!it.IsEditing)
					{
						var body = string.IsNullOrWhiteSpace(it.Text) ? "RimAI.Common.Empty".Translate().ToString() : it.Text;
						Widgets.Label(contentRect, body);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Text; }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { if (!recap.DeleteRecap(convKey, it.Id)) Verse.Log.Warning("[RimAI.Core][P10] DeleteRecap failed"); else ReloadRecaps(recap, convKey); }
					}
					else
					{
						it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { if (!recap.UpdateRecap(convKey, it.Id, it.EditText ?? string.Empty)) Verse.Log.Warning("[RimAI.Core][P10] UpdateRecap failed"); else { it.Text = it.EditText; it.IsEditing = false; } }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Text; }
					}
					y += rowH + 6f;
				}
			}
			Widgets.EndScrollView();
		}

		private void EnsureRecapEventHooked(IRecapService recap, string convKey)
		{
			try
			{
				if (recap == null || string.IsNullOrWhiteSpace(convKey)) return;
				if (ReferenceEquals(_recapHooked, recap) && string.Equals(_recapHookedConvKey, convKey, StringComparison.Ordinal)) return;
				TryUnhookRecapEvent();
				_recapHooked = recap;
				_recapHookedConvKey = convKey;
				_recapHandler = (ck, id) => { if (string.Equals(ck, _recapHookedConvKey, StringComparison.Ordinal)) _recapDirty = true; };
				_recapHooked.OnRecapUpdated += _recapHandler;
			}
			catch { }
		}

		private void TryUnhookRecapEvent()
		{
			try
			{
				if (_recapHooked != null && _recapHandler != null)
				{
					_recapHooked.OnRecapUpdated -= _recapHandler;
				}
			}
			catch { }
			finally
			{
				_recapHooked = null;
				_recapHandler = null;
				_recapHookedConvKey = null;
				_recapDirty = false;
			}
		}

		private List<string> _relatedConvLabels;
		private bool _relatedLabelsResolving = false;

		private void EnsureRelatedLoaded(IHistoryService history, IReadOnlyList<string> participantIds)
		{
			if (_relatedConvs != null) return;
			_relatedConvs = new List<string>();
			string pawnKey = null;
			try { if (participantIds != null) { foreach (var id in participantIds) { if (id != null && id.StartsWith("pawn:")) { pawnKey = id; break; } } } } catch { }
			if (string.IsNullOrEmpty(pawnKey)) return;
			try
			{
				var all = history.GetAllConvKeys();
				foreach (var ck in all)
				{
					try { var parts = history.GetParticipantsOrEmpty(ck); if (parts != null) { foreach (var p in parts) { if (string.Equals(p, pawnKey, StringComparison.Ordinal)) { _relatedConvs.Add(ck); break; } } } } catch { }
				}
			}
			catch { }
			BeginResolveRelatedLabels(history);
		}

		private void BeginResolveRelatedLabels(IHistoryService history)
		{
			if (_relatedConvs == null || _relatedLabelsResolving) return;
			_relatedLabelsResolving = true;
			_relatedConvLabels = new List<string>(new string[_relatedConvs.Count]);
			_ = System.Threading.Tasks.Task.Run(async () =>
			{
				try
				{
					var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
					for (int i = 0; i < _relatedConvs.Count; i++)
					{
						string ck = _relatedConvs[i];
						string label = ck;
						try
						{
							var parts = history.GetParticipantsOrEmpty(ck) ?? new System.Collections.Generic.List<string>();
							var names = new System.Collections.Generic.List<string>();
							foreach (var p in parts)
							{
								if (string.IsNullOrWhiteSpace(p)) continue;
								if (p.StartsWith("player:")) continue;
								if (p.StartsWith("pawn:"))
								{
									var s = p.Substring("pawn:".Length);
									if (int.TryParse(s, out var id))
									{
										try { var snap = await world.GetPawnPromptSnapshotAsync(id); var nm = snap?.Id?.Name; names.Add(string.IsNullOrWhiteSpace(nm) ? p : nm); }
										catch { names.Add(p); }
									}
									else { names.Add(p); }
								}
								else { names.Add(p); }
							}
							label = names.Count > 0 ? string.Join(", ", names) : ck;
						}
						catch { }
						_relatedConvLabels[i] = label;
					}
				}
				finally { _relatedLabelsResolving = false; }
			});
		}

		private void DrawRelated(Rect rect, Action<string> switchToConvKey)
		{
			float y = rect.y;
			// 选择按钮
			string currentLabel = (_relatedSelectedIdx >= 0 && _relatedSelectedIdx < (_relatedConvs?.Count ?? 0))
				? ((_relatedConvLabels != null && _relatedConvLabels.Count == _relatedConvs.Count) ? _relatedConvLabels[_relatedSelectedIdx] : _relatedConvs[_relatedSelectedIdx])
				: "RimAI.ChatUI.Related.Select".Translate();
			if (Widgets.ButtonText(new Rect(rect.x, y, 240f, 28f), currentLabel))
			{
				var menu = new List<FloatMenuOption>();
				if (_relatedConvs != null)
				{
					for (int i = 0; i < _relatedConvs.Count; i++)
					{
						int idx = i; var ck = _relatedConvs[i];
						var label = (_relatedConvLabels != null && _relatedConvLabels.Count == _relatedConvs.Count) ? _relatedConvLabels[i] : ck;
						menu.Add(new FloatMenuOption(label, () => { _relatedSelectedIdx = idx; }));
					}
				}
				if (menu.Count > 0) Find.WindowStack.Add(new FloatMenu(menu));
			}
			y += 34f;
			// 即时显示所选关联对话的历史界面（不跳转主界面）
			if (_relatedSelectedIdx >= 0 && _relatedSelectedIdx < (_relatedConvs?.Count ?? 0))
			{
				string target = _relatedConvs[_relatedSelectedIdx];
				// 内嵌一个只读的历史线程视图（使用相同绘制逻辑，但不影响当前会话）
				var innerRect = new Rect(rect.x, y + 6f, rect.width, rect.height - (y - rect.y) - 6f);
				DrawRelatedThreadInline(innerRect, target);
			}
		}

		private void DrawRelatedThreadInline(Rect rect, string convKey)
		{
			// 构建临时 entries 快照
			try
			{
				var history = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IHistoryService>();
				var thread = history.GetThreadAsync(convKey, 1, 200).GetAwaiter().GetResult();
				var temp = new List<HistoryEntryVM>();
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						temp.Add(new HistoryEntryVM { Id = e.Id, Role = e.Role, Content = e.Content, TimestampUtc = e.Timestamp, IsEditing = false, EditText = e.Content });
					}
				}
				// 复用 Thread 绘制逻辑（只读：隐藏保存/删除，保留修改/删除按钮入口，但点击时操作当前 convKey）
				var saved = _entries; _entries = temp;
				DrawThread(rect, history, convKey);
				_entries = saved;
			}
			catch { }
		}

		private (string userName, string pawnName) GetOrBeginResolveNames(IHistoryService history, string convKey)
		{
			if (string.IsNullOrWhiteSpace(convKey)) return ("玩家", "Pawn");
			if (!_convUserName.TryGetValue(convKey, out var user)) user = null;
			if (!_convPawnName.TryGetValue(convKey, out var pawn)) pawn = null;
			if ((user == null || pawn == null) && !_nameResolving.Contains(convKey))
			{
				_nameResolving.Add(convKey);
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try
					{
						var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
						var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
						string playerTitle = cfg?.GetPlayerTitleOrDefault() ?? "玩家";
						string pawnName = "Pawn";
						try
						{
							var parts = history.GetParticipantsOrEmpty(convKey) ?? new System.Collections.Generic.List<string>();
							foreach (var p in parts)
							{
								if (p != null && p.StartsWith("pawn:"))
								{
									var s = p.Substring("pawn:".Length);
									if (int.TryParse(s, out var id))
									{
										try { var snap = await world.GetPawnPromptSnapshotAsync(id); var nm = snap?.Id?.Name; if (!string.IsNullOrWhiteSpace(nm)) pawnName = nm; }
										catch { }
									}
									break;
								}
							}
						}
						catch { }
						lock (_convUserName)
						{
							_convUserName[convKey] = playerTitle;
							_convPawnName[convKey] = pawnName;
						}
					}
					finally { _nameResolving.Remove(convKey); }
				});
			}
			return (user ?? "RimAI.Common.Player".Translate(), pawn ?? "RimAI.Common.Pawn".Translate());
		}

		public void ClearCache()
		{
			_entries = null; _recaps = null; _relatedConvs = null; _relatedSelectedIdx = -1; TryUnhookRecapEvent();
		}
	}
}


