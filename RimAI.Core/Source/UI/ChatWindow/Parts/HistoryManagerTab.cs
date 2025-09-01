using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class HistoryManagerTabView
	{
		private enum HistorySubTab { Thread, Recaps, Related, RawJson }
		private HistorySubTab _subTab = HistorySubTab.Thread;
		private Vector2 _scrollThread = Vector2.zero;
		private Vector2 _scrollRecaps = Vector2.zero;
		private Vector2 _scrollRelated = Vector2.zero;
		private Vector2 _scrollRaw = Vector2.zero;
		private string _rawAllText;
		private bool _rawLoaded;
		private bool _recapGenerating = false;

		private sealed class HistoryEntryVM { public string Id; public string ConvKey; public EntryRole Role; public string Content; public DateTime TimestampUtc; public bool IsEditing; public string EditText; }
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
		// 关联对话内联编辑缓存（convKey → 正在编辑的 entryId 集合 / 文本）
		private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>> _relatedEditingIds = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<string>>(System.StringComparer.Ordinal);
		private readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> _relatedEditTexts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>(System.StringComparer.Ordinal);
		// 每条记录的 speaker 映射（entryId → speakerId，例如 player:xxx / pawn:123 / agent:stage）
		private readonly System.Collections.Generic.Dictionary<string, string> _entrySpeakerById = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
		// 每个 speakerId 的显示名缓存（speakerId → 显示名）
		private readonly System.Collections.Generic.Dictionary<string, string> _speakerDisplayName = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
		private readonly System.Collections.Generic.HashSet<string> _speakerResolving = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);

		public void Draw(Rect inRect, RimAI.Core.Source.UI.ChatWindow.ChatConversationState state, IHistoryService history, IRecapService recap)
		{
			float tabsH = 28f; float sp = 6f; float btnW = 110f;
			var rTabs = new Rect(inRect.x, inRect.y, inRect.width, tabsH);
			if (Widgets.ButtonText(new Rect(rTabs.x, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Thread".Translate())) _subTab = HistorySubTab.Thread;
			if (Widgets.ButtonText(new Rect(rTabs.x + btnW + sp, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Recaps".Translate())) _subTab = HistorySubTab.Recaps;
			if (Widgets.ButtonText(new Rect(rTabs.x + (btnW + sp) * 2, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.Related".Translate())) _subTab = HistorySubTab.Related;
			if (Widgets.ButtonText(new Rect(rTabs.x + (btnW + sp) * 3, rTabs.y, btnW, tabsH), "RimAI.ChatUI.History.Tab.RawJson".Translate())) { _subTab = HistorySubTab.RawJson; _rawLoaded = false; _rawAllText = string.Empty; }
			// Add right aligned button for viewing raw JSON (thread-wide), only on Thread subtab
			var rightBtnW = 220f;
			var rightBtnRect = new Rect(rTabs.xMax - rightBtnW, rTabs.y, rightBtnW, tabsH);
			// 保留右侧空间（不再使用子窗口入口）
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
					DrawRelated(contentRect, history, state.ParticipantIds);
					break;
				case HistorySubTab.RawJson:
					DrawRawJson(contentRect, history, state.ConvKey);
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
						_entries.Add(new HistoryEntryVM { Id = e.Id, ConvKey = convKey, Role = e.Role, Content = e.Content, TimestampUtc = e.Timestamp, IsEditing = false, EditText = e.Content });
					}
				}
				// 过滤后台日志/标签类条目（不在 UI 显示，例如 end:Completed/Aborted 等）
				try
				{
					var rawList = history.GetAllEntriesRawAsync(convKey).GetAwaiter().GetResult();
					if (rawList != null && rawList.Count > 0)
					{
						var hideIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
						for (int i = 0; i < rawList.Count; i++)
						{
							var r = rawList[i];
							if (r == null || r.Deleted) continue;
							try
							{
								var jo = Newtonsoft.Json.Linq.JObject.Parse(r.Content ?? "{}");
								var type = jo.Value<string>("type") ?? string.Empty;
								var content = jo.Value<string>("content") ?? string.Empty;
								if (string.Equals(type, "log", System.StringComparison.OrdinalIgnoreCase)) { if (!string.IsNullOrEmpty(r.Id)) hideIds.Add(r.Id); continue; }
								if (!string.IsNullOrEmpty(content) && (content.StartsWith("end:", System.StringComparison.OrdinalIgnoreCase) || content.StartsWith("parseError:", System.StringComparison.OrdinalIgnoreCase)))
								{
									if (!string.IsNullOrEmpty(r.Id)) hideIds.Add(r.Id);
								}
							}
							catch { }
						}
						if (hideIds.Count > 0 && _entries != null)
						{
							for (int i = _entries.Count - 1; i >= 0; i--) { var it = _entries[i]; if (it != null && !string.IsNullOrEmpty(it.Id) && hideIds.Contains(it.Id)) _entries.RemoveAt(i); }
						}
					}
				}
				catch { }
				// 同步加载原始 JSON 以提取每条记录的 speaker（用于群聊时逐条显示正确人物名）
				try
				{
					var rawList = history.GetAllEntriesRawAsync(convKey).GetAwaiter().GetResult();
					if (rawList != null)
					{
						for (int i = 0; i < rawList.Count; i++)
						{
							var r = rawList[i];
							if (r == null || r.Deleted) continue;
							try
							{
								var jo = Newtonsoft.Json.Linq.JObject.Parse(r.Content ?? "{}");
								var sp = jo.Value<string>("speaker") ?? string.Empty;
								if (!string.IsNullOrWhiteSpace(r.Id) && !_entrySpeakerById.ContainsKey(r.Id)) _entrySpeakerById[r.Id] = sp ?? string.Empty;
							}
							catch { }
						}
					}
				}
				catch { }
				// 异步解析本会话中出现的各个 pawn: 的显示名
				BeginResolveSpeakerNamesForConv(history, convKey);
			}
			catch { _entries = new List<HistoryEntryVM>(); }
		}

		private void ReloadHistory(IHistoryService history, string convKey)
		{
			_entries = null; EnsureHistoryLoaded(history, convKey);
			// 同步刷新 Raw JSON 视图的缓存标志，确保删除/编辑后原文页面能重新加载
			_rawLoaded = false; _rawAllText = string.Empty;
		}

		public void ForceReloadHistory(IHistoryService history, string convKey)
		{
			ReloadHistory(history, convKey);
		}

		private void DrawThread(Rect rect, IHistoryService history, string convKey)
		{
			// 先计算总高度以启用滚动
			float totalH = 4f;
			// 顶部操作区（清空内容按钮）高度
			totalH += 34f;
			float actionsWForMeasure = 200f;
			float contentWForMeasure = (rect.width - 16f) - actionsWForMeasure - 16f;
			var names = GetOrBeginResolveNames(history, convKey);
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var it = _entries[i];
					var measureRect = new Rect(0f, 0f, contentWForMeasure, 99999f);
					string senderNameMeasure = it.Role == EntryRole.User ? names.userName : ResolveDisplayNameForEntry(it.Id, names.pawnName);
					string labelForMeasure = (senderNameMeasure ?? string.Empty) + ": " + (it.Content ?? string.Empty);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(labelForMeasure, measureRect.width));
					float rowH = contentH + 12f;
					totalH += rowH + 6f;
				}
			}
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref _scrollThread, viewRect);
			float y = 4f;
			// 顶部操作区：清空当前对话全部内容（真实删除会话）
			var clearBtnRect = new Rect(0f, y, 160f, 28f);
			var prev = GUI.color; GUI.color = Color.red;
			if (Widgets.ButtonText(clearBtnRect, "RimAI.Common.Clear".Translate()))
			{
				try { var ok = history.ClearThreadAsync(convKey).GetAwaiter().GetResult(); if (ok) { ReloadHistory(history, convKey); } }
				catch { }
			}
			GUI.color = prev;
			y += 34f;
			if (_entries != null)
			{
				for (int i = 0; i < _entries.Count; i++)
				{
					var it = _entries[i];
					float actionsW = 200f;
					float contentW = viewRect.width - actionsW - 16f;
					var contentMeasureRect = new Rect(0f, 0f, contentW, 99999f);
					string senderName = it.Role == EntryRole.User ? names.userName : ResolveDisplayNameForEntry(it.Id, names.pawnName);
					string label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, contentMeasureRect.width)) : Mathf.Max(24f, Text.CalcHeight(label, contentMeasureRect.width));
					float rowH = contentH + 12f;
					var row = new Rect(0f, y, viewRect.width, rowH);
					Widgets.DrawHighlightIfMouseover(row);
					var contentRect = new Rect(row.x + 6f, row.y + 6f, contentW, contentH);
					var actionsRect = new Rect(row.xMax - (actionsW + 10f), row.y + 8f, actionsW, 28f);
					label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
					if (!it.IsEditing)
					{
						Widgets.Label(contentRect, label);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Content; }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { _ = DeleteEntryAsync(history, it.ConvKey ?? convKey, it.Id); }
					}
					else
					{
						it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { _ = SaveEntryAsync(history, it.ConvKey ?? convKey, it.Id, it.EditText); }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Content; }
					}
					y += rowH + 6f;
				}
			}
			Widgets.EndScrollView();
		}

		// 单条 JSON 查看窗口已移除

		private sealed class JsonHistoryViewerDialogAll : Window
		{
			private readonly IHistoryService _history;
			private readonly string _convKey;
			private string _rawJson = string.Empty;
			private Vector2 _scroll = Vector2.zero;
			private bool _loaded;
			public override Vector2 InitialSize => new Vector2(900f, 600f);

			public JsonHistoryViewerDialogAll(IHistoryService history, string convKey)
			{
				_history = history;
				_convKey = convKey;
				doCloseX = true;
				draggable = true;
				absorbInputAroundWindow = true;
				closeOnCancel = true;
				closeOnClickedOutside = false;
			}

			public override void DoWindowContents(Rect inRect)
			{
				if (!_loaded)
				{
					_loaded = true;
					_ = System.Threading.Tasks.Task.Run(() =>
					{
						try
						{
							var rawList = _history.GetAllEntriesRawAsync(_convKey).GetAwaiter().GetResult();
							var sb = new System.Text.StringBuilder();
							if (rawList != null)
							{
								for (int i = 0; i < rawList.Count; i++)
								{
									var r = rawList[i];
									if (r == null || r.Deleted) continue;
									sb.AppendLine(r.Content ?? string.Empty);
								}
							}
							_rawJson = sb.ToString();
						}
						catch { _rawJson = string.Empty; }
					});
				}

				float footerH = 28f; float btnW = 90f;
				var footer = new Rect(inRect.x, inRect.yMax - footerH, inRect.width, footerH);
				var btnClose = new Rect(footer.xMax - btnW, footer.y, btnW, footerH);

				var body = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - footerH - 6f);
				var inner = new Rect(0f, 0f, body.width - 16f, Mathf.Max(body.height, Text.CalcHeight(_rawJson ?? string.Empty, body.width - 16f) + 10f));
				Widgets.BeginScrollView(body, ref _scroll, inner);
				_rawJson = Widgets.TextArea(new Rect(0f, 0f, inner.width, inner.height), _rawJson ?? string.Empty);
				Widgets.EndScrollView();

				// 禁用保存/导出功能：仅允许查看
				// if (Widgets.ButtonText(btnClose, "RimAI.Common.Close".Translate())) Close();
			}
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

		private void DrawRelated(Rect rect, IHistoryService history, System.Collections.Generic.IReadOnlyList<string> currentParticipantIds)
		{
			float y = rect.y;
			// 选择按钮
			string currentLabel = (_relatedSelectedIdx >= 0 && _relatedSelectedIdx < (_relatedConvs?.Count ?? 0))
				? ((_relatedConvLabels != null && _relatedConvLabels.Count == _relatedConvs.Count) ? _relatedConvLabels[_relatedSelectedIdx] : _relatedConvs[_relatedSelectedIdx])
				: "RimAI.ChatUI.Related.Select".Translate();
			var selectBtnRect = new Rect(rect.x, y, 240f, 28f);
			if (Widgets.ButtonText(selectBtnRect, currentLabel))
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
			// 清空内容按钮（仅清空当前选中的关联会话）
			var clearBtnRect = new Rect(selectBtnRect.xMax + 10f, y, 160f, 28f);
			var prevColor = GUI.color;
			GUI.color = Color.red;
			if (Widgets.ButtonText(clearBtnRect, "RimAI.ChatUI.Related.ClearSelected".Translate()))
			{
				try
				{
					if (_relatedSelectedIdx >= 0 && _relatedSelectedIdx < (_relatedConvs?.Count ?? 0))
					{
						var ck = _relatedConvs[_relatedSelectedIdx];
						var ok = history.ClearThreadAsync(ck).GetAwaiter().GetResult();
						if (ok)
						{
							// 刷新关联会话列表与当前视图（并移除下拉中的该键）
							_relatedConvs = null;
							_relatedConvLabels = null;
							_relatedLabelsResolving = false;
							EnsureRelatedLoaded(history, currentParticipantIds);
							_relatedSelectedIdx = -1;
						}
					}
				}
				catch { }
			}
			GUI.color = prevColor;
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
				// 取/建编辑缓存
				if (!_relatedEditingIds.TryGetValue(convKey, out var editingSet)) { editingSet = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal); _relatedEditingIds[convKey] = editingSet; }
				if (!_relatedEditTexts.TryGetValue(convKey, out var editTexts)) { editTexts = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal); _relatedEditTexts[convKey] = editTexts; }
				var thread = history.GetThreadAsync(convKey, 1, 200).GetAwaiter().GetResult();
				var temp = new List<HistoryEntryVM>();
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						bool isEditing = (e.Id != null && editingSet.Contains(e.Id));
						string editText = e.Content;
						if (isEditing && e.Id != null && editTexts.TryGetValue(e.Id, out var saved)) editText = saved ?? e.Content;
						temp.Add(new HistoryEntryVM { Id = e.Id, ConvKey = convKey, Role = e.Role, Content = e.Content, TimestampUtc = e.Timestamp, IsEditing = isEditing, EditText = editText });
					}
				}
				// 解析该关联会话的原始 JSON，提取每条记录的 speaker → 供逐条显示正确的人名
				try
				{
					var rawList = history.GetAllEntriesRawAsync(convKey).GetAwaiter().GetResult();
					if (rawList != null)
					{
						for (int i = 0; i < rawList.Count; i++)
						{
							var r = rawList[i];
							if (r == null || r.Deleted) continue;
							try
							{
								var jo = Newtonsoft.Json.Linq.JObject.Parse(r.Content ?? "{}");
								var sp = jo.Value<string>("speaker") ?? string.Empty;
								if (!string.IsNullOrWhiteSpace(r.Id)) _entrySpeakerById[r.Id] = sp ?? string.Empty;
							}
							catch { }
						}
					}
				}
				catch { }
				// 启动一次该会话的显示名解析（玩家称谓/小人名）
				BeginResolveSpeakerNamesForConv(history, convKey);
				// 在内联区域完整实现与 Thread 相同的“编辑/保存/删除”行为（直接操作 target convKey）
				float totalH = 4f; float actionsW = 200f; float contentW = rect.width - 16f - actionsW - 16f;
				// 动态高度测算
				if (temp != null)
				{
					for (int i = 0; i < temp.Count; i++)
					{
						var it = temp[i]; var measure = new Rect(0f, 0f, contentW, 99999f);
						string senderName = it.Role == EntryRole.User ? (GetOrBeginResolveNames(history, convKey).userName) : ResolveDisplayNameForEntry(it.Id, GetOrBeginResolveNames(history, convKey).pawnName);
						string label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
						float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measure.width)) : Mathf.Max(24f, Text.CalcHeight(label, measure.width));
						totalH += contentH + 12f + 6f;
					}
				}
				var view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
				Widgets.BeginScrollView(rect, ref _scrollRelated, view);
				float y = 4f;
				if (temp != null)
				{
					for (int i = 0; i < temp.Count; i++)
					{
						var it = temp[i];
						float contentH;
						var contentRect = new Rect(6f, y + 6f, contentW, 99999f);
						var measure = new Rect(0f, 0f, contentW, 99999f);
						string senderName = it.Role == EntryRole.User ? (GetOrBeginResolveNames(history, convKey).userName) : ResolveDisplayNameForEntry(it.Id, GetOrBeginResolveNames(history, convKey).pawnName);
						string label = (senderName ?? string.Empty) + ": " + (it.Content ?? string.Empty);
						contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measure.width)) : Mathf.Max(24f, Text.CalcHeight(label, measure.width));
						var row = new Rect(0f, y, view.width, contentH + 12f);
						Widgets.DrawHighlightIfMouseover(row);
						var actionsRect = new Rect(row.xMax - (actionsW + 10f), row.y + 8f, actionsW, 28f);
						if (!it.IsEditing)
						{
							Widgets.Label(new Rect(contentRect.x, contentRect.y, contentW, contentH), label);
							if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { if (!string.IsNullOrEmpty(it.Id)) { editingSet.Add(it.Id); editTexts[it.Id] = it.Content; it.IsEditing = true; it.EditText = it.Content; } }
							if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { _ = DeleteEntryAsync(history, convKey, it.Id); }
						}
						else
						{
							it.EditText = Widgets.TextArea(new Rect(contentRect.x, contentRect.y, contentW, contentH), it.EditText ?? string.Empty);
							if (!string.IsNullOrEmpty(it.Id)) editTexts[it.Id] = it.EditText ?? string.Empty;
							if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate()))
							{
								_ = SaveEntryAsync(history, convKey, it.Id, it.EditText);
								it.IsEditing = false; it.Content = it.EditText;
								if (!string.IsNullOrEmpty(it.Id)) { editingSet.Remove(it.Id); editTexts.Remove(it.Id); }
							}
							if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate()))
							{
								it.IsEditing = false; it.EditText = it.Content;
								if (!string.IsNullOrEmpty(it.Id)) { editingSet.Remove(it.Id); editTexts.Remove(it.Id); }
							}
						}
						y += contentH + 12f + 6f;
					}
				}
				Widgets.EndScrollView();
			}
			catch { }
		}

		private void DrawRawJson(Rect rect, IHistoryService history, string convKey)
		{
			if (!_rawLoaded)
			{
				_rawLoaded = true;
				_rawAllText = string.Empty;
				try
				{
					var list = history.GetAllEntriesRawAsync(convKey).GetAwaiter().GetResult();
					var sb = new System.Text.StringBuilder();
					if (list != null)
					{
						for (int i = 0; i < list.Count; i++)
						{
							var r = list[i];
							if (r == null || r.Deleted) continue;
							sb.AppendLine(r.Content ?? string.Empty);
						}
					}
					_rawAllText = sb.ToString();
				}
				catch { _rawAllText = string.Empty; }
			}

			// 可滚动富文本框
			float totalH = Mathf.Max(24f, Text.CalcHeight(_rawAllText ?? string.Empty, rect.width - 16f));
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH + 8f));
			Widgets.BeginScrollView(rect, ref _scrollRaw, viewRect);
			var textRect = new Rect(4f, 4f, viewRect.width - 8f, totalH);
			_rawAllText = Widgets.TextArea(textRect, _rawAllText ?? string.Empty);
			Widgets.EndScrollView();
		}

		private (string userName, string pawnName) GetOrBeginResolveNames(IHistoryService history, string convKey)
		{
			if (string.IsNullOrWhiteSpace(convKey)) return ("RimAI.Common.Player".Translate(), "RimAI.Common.Pawn".Translate());
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
						var loc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<ILocalizationService>();
						var locale = cfg?.GetInternal()?.General?.Locale ?? "en";
						string playerTitle = cfg?.GetPlayerTitleOrDefault();
						if (string.IsNullOrWhiteSpace(playerTitle))
						{
							playerTitle = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
						}
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

		private void BeginResolveSpeakerNamesForConv(IHistoryService history, string convKey)
		{
			try
			{
				var parts = history.GetParticipantsOrEmpty(convKey) ?? System.Array.Empty<string>();
				var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
				var cfg = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var loc = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<ILocalizationService>();
				var locale = cfg?.GetInternal()?.General?.Locale ?? "en";
				string playerTitle = cfg?.GetPlayerTitleOrDefault();
				if (string.IsNullOrWhiteSpace(playerTitle))
				{
					playerTitle = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
				}
				foreach (var p in parts)
				{
					if (string.IsNullOrWhiteSpace(p)) continue;
					if (_speakerDisplayName.ContainsKey(p)) continue;
					if (_speakerResolving.Contains(p)) continue;
					_speakerResolving.Add(p);
					_ = System.Threading.Tasks.Task.Run(async () =>
					{
						try
						{
							string name = null;
							if (p.StartsWith("player:"))
							{
								name = playerTitle;
							}
							else if (p.StartsWith("pawn:"))
							{
								var sid = p.Substring("pawn:".Length);
								if (int.TryParse(sid, out var id))
								{
									try { var snap = await world.GetPawnPromptSnapshotAsync(id); var nm = snap?.Id?.Name; name = string.IsNullOrWhiteSpace(nm) ? "RimAI.Common.Pawn".Translate().ToString() : nm; }
									catch { name = "RimAI.Common.Pawn".Translate().ToString(); }
								}
							}
							else if (p.StartsWith("agent:stage"))
							{
								name = "Stage";
							}
							lock (_speakerDisplayName) { _speakerDisplayName[p] = name ?? p; }
						}
						finally { _speakerResolving.Remove(p); }
					});
				}
			}
			catch { }
		}

		private string ResolveDisplayNameForEntry(string entryId, string defaultAiName)
		{
			try
			{
				if (!string.IsNullOrWhiteSpace(entryId) && _entrySpeakerById.TryGetValue(entryId, out var speaker))
				{
					if (!string.IsNullOrWhiteSpace(speaker))
					{
						if (_speakerDisplayName.TryGetValue(speaker, out var nm)) return nm ?? defaultAiName;
						// 未解析完成则返回默认 AI 名
					}
				}
			}
			catch { }
			return defaultAiName;
		}

		public void ClearCache()
		{
			_entries = null; _recaps = null; _relatedConvs = null; _relatedSelectedIdx = -1; TryUnhookRecapEvent();
		}
	}
}


