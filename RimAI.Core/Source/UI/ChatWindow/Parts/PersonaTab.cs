using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.Persona.Biography;
using RimAI.Core.Source.Modules.Persona.Ideology;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal sealed class PersonaTabView
	{
		private enum PersonaSubTab { Biography, Ideology }
		private PersonaSubTab _subTab = PersonaSubTab.Biography;

		private Vector2 _scrollBio = Vector2.zero;
		private Vector2 _scrollIdeo = Vector2.zero;

		// Biography state
		private sealed class BiographyItemVM { public string Id; public string Text; public string Source; public bool IsEditing; public string EditText; public DateTime UpdatedAtUtc; }
		private List<BiographyItemVM> _bioItems;
		private bool _bioGenerating = false;

		// Ideology state
		private string _ideologyText; // 展示为一个富文本区域（四段合并）
		private bool _ideoBusy = false;
		private bool? _autoEnabled; // 单一开关：纳入自动更新
		private string _lastEntityId;

		public void Draw(Rect inRect, string entityId, string convKey, IPersonaService persona, IBiographyService biography, IIdeologyService ideology)
		{
			var prevFont = Text.Font;
			Text.Font = GameFont.Small;
			try
			{
				if (!string.Equals(_lastEntityId, entityId, System.StringComparison.Ordinal)) { _autoEnabled = null; _lastEntityId = entityId; }
				// 顶部页签
				float tabsH = 28f; float sp = 6f; float btnW = 110f;
				var rTabs = new Rect(inRect.x, inRect.y, inRect.width, tabsH);
				if (Widgets.ButtonText(new Rect(rTabs.x, rTabs.y, btnW, tabsH), "RimAI.ChatUI.Persona.Tab.Bio".Translate())) _subTab = PersonaSubTab.Biography;
				if (Widgets.ButtonText(new Rect(rTabs.x + btnW + sp, rTabs.y, btnW, tabsH), "RimAI.ChatUI.Persona.Tab.Ideo".Translate())) _subTab = PersonaSubTab.Ideology;
				var contentRect = new Rect(inRect.x, rTabs.yMax + 8f, inRect.width, inRect.height - tabsH - 12f);

				if (string.IsNullOrWhiteSpace(entityId))
				{
					Widgets.Label(contentRect, "RimAI.ChatUI.Common.NoPawnSelected".Translate());
					return;
				}

				switch (_subTab)
				{
					case PersonaSubTab.Biography:
						EnsureBiographyLoaded(biography, entityId);
						DrawBiography(contentRect, biography, entityId);
						break;
					case PersonaSubTab.Ideology:
						EnsureIdeologyLoaded(ideology, entityId);
						DrawIdeology(contentRect, ideology, entityId);
						break;
				}
			}
			finally
			{
				Text.Font = prevFont;
			}
		}

		private void EnsureBiographyLoaded(IBiographyService biography, string entityId)
		{
			if (_bioItems != null) return;
			try
			{
				var list = biography.List(entityId) ?? new List<RimAI.Core.Source.Modules.Persona.BiographyItem>();
				_bioItems = new List<BiographyItemVM>();
				foreach (var it in list)
				{
					_bioItems.Add(new BiographyItemVM { Id = it.Id, Text = it.Text, Source = it.Source, IsEditing = false, EditText = it.Text, UpdatedAtUtc = it.UpdatedAtUtc });
				}
			}
			catch { _bioItems = new List<BiographyItemVM>(); }
		}

		private void ReloadBiography(IBiographyService biography, string entityId)
		{
			_bioItems = null; EnsureBiographyLoaded(biography, entityId);
		}

		private void DrawBiography(Rect rect, IBiographyService biography, string entityId)
		{
			// 动态高度：生成按钮行 + 项列表
			float totalH = 4f;
			float actionsW = 200f;
			float contentW = (rect.width - 16f) - actionsW - 16f;
			if (_bioItems != null)
			{
				totalH += 34f; // 生成按钮行
				for (int i = 0; i < _bioItems.Count; i++)
				{
					var it = _bioItems[i];
					var measureRect = new Rect(0f, 0f, contentW, 99999f);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
					float headerH = 22f;
					float headerPad = 2f;
					float rowH = headerH + headerPad + contentH + 12f;
					totalH += rowH + 6f;
				}
			}
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, totalH));
			Widgets.BeginScrollView(rect, ref _scrollBio, viewRect);
			float y = 4f;
			if (_bioItems != null)
			{
				// 生成草案按钮
				if (!_bioGenerating && Widgets.ButtonText(new Rect(viewRect.x, y, 160f, 28f), "RimAI.ChatUI.Persona.GenerateDraft".Translate()))
				{
					_bioGenerating = true;
					_ = Task.Run(async () =>
					{
						try
						{
							var drafts = await biography.GenerateDraftAsync(entityId);
							if (drafts != null)
							{
								foreach (var d in drafts)
								{
									try { biography.Upsert(entityId, d); } catch { }
								}
							}
						}
						catch (Exception ex) { try { Verse.Log.Warning($"[RimAI.Core][P10] Biography.GenerateDraft failed entity={entityId}: {ex.Message}"); } catch { } }
						finally { _bioGenerating = false; ReloadBiography(biography, entityId); }
					});
				}
				if (_bioGenerating)
				{
					Widgets.Label(new Rect(viewRect.x + 170f, y + 4f, 200f, 22f), "RimAI.ChatUI.Recap.Generating".Translate());
				}
				// 自动更新勾选
				try
				{
					var settings = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Persona.IPersonaAutoSettingsService>();
					if (_autoEnabled == null)
					{
						bool eb = settings?.GetAutoBio(entityId) ?? false;
						bool ei = settings?.GetAutoIdeo(entityId) ?? false;
						_autoEnabled = eb || ei;
					}
					bool val = _autoEnabled ?? false;
					Widgets.CheckboxLabeled(new Rect(viewRect.x + 240f, y, 140f, 28f), "RimAI.Common.AutoUpdate".Translate(), ref val);
					if (val != (_autoEnabled ?? false)) { settings?.SetAutoBio(entityId, val); settings?.SetAutoIdeo(entityId, val); _autoEnabled = val; }
				}
				catch { }
				y += 34f;
				for (int i = 0; i < _bioItems.Count; i++)
				{
					var it = _bioItems[i];
					var measureRect = new Rect(0f, 0f, contentW, 99999f);
					float contentH = it.IsEditing ? Mathf.Max(28f, Text.CalcHeight(it.EditText ?? string.Empty, measureRect.width)) : Mathf.Max(24f, Text.CalcHeight(it.Text ?? string.Empty, measureRect.width));
					float headerH = 22f;
					float headerPad = 2f;
					float rowH = headerH + headerPad + contentH + 12f;
					var row = new Rect(0f, y, viewRect.width, rowH);
					Widgets.DrawHighlightIfMouseover(row);
					var headerRect = new Rect(row.x + 6f, row.y + 6f, contentW, headerH);
					var contentRect = new Rect(row.x + 6f, headerRect.yMax + headerPad, contentW, contentH);
					var actionsRect = new Rect(row.xMax - 210f, row.y + 8f, 200f, row.height - 16f);
					// 标题行（来源+时间）
					Widgets.Label(headerRect, $"[{(string.IsNullOrWhiteSpace(it.Source) ? "-" : it.Source)}] {it.UpdatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}");
					if (!it.IsEditing)
					{
						var body = string.IsNullOrWhiteSpace(it.Text) ? "RimAI.Common.Empty".Translate().ToString() : it.Text;
						Widgets.Label(contentRect, body);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Edit".Translate())) { it.IsEditing = true; it.EditText = it.Text; }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Delete".Translate())) { try { biography.Remove(entityId, it.Id); } catch { } ReloadBiography(biography, entityId); }
					}
					else
					{
						it.EditText = Widgets.TextArea(contentRect, it.EditText ?? string.Empty);
						if (Widgets.ButtonText(new Rect(actionsRect.x, actionsRect.y, 90f, 28f), "RimAI.Common.Save".Translate())) { try { biography.Upsert(entityId, new RimAI.Core.Source.Modules.Persona.BiographyItem { Id = it.Id, Text = it.EditText ?? string.Empty, Source = string.IsNullOrWhiteSpace(it.Source) ? "user" : it.Source }); } catch { } it.Text = it.EditText; it.IsEditing = false; ReloadBiography(biography, entityId); }
						if (Widgets.ButtonText(new Rect(actionsRect.x + 100f, actionsRect.y, 90f, 28f), "RimAI.Common.Cancel".Translate())) { it.IsEditing = false; it.EditText = it.Text; }
					}
					y += rowH + 6f;
				}
			}
			Widgets.EndScrollView();
		}

		private void EnsureIdeologyLoaded(IIdeologyService ideology, string entityId)
		{
			if (_ideologyText != null) return;
			try
			{
				var snap = ideology.Get(entityId) ?? new RimAI.Core.Source.Modules.Persona.IdeologySnapshot();
				_ideologyText = ComposeIdeologyText(snap);
			}
			catch { _ideologyText = string.Empty; }
		}

		private void DrawIdeology(Rect rect, IIdeologyService ideology, string entityId)
		{
			// 顶部操作按钮：生成/保存
			float y = rect.y;
			if (!_ideoBusy && Widgets.ButtonText(new Rect(rect.x, y, 90f, 28f), "RimAI.ChatUI.Recap.Generate".Translate()))
			{
				_ideoBusy = true;
				_ = Task.Run(async () =>
				{
					try
					{
						var s = await ideology.GenerateAsync(entityId);
						s = s ?? new RimAI.Core.Source.Modules.Persona.IdeologySnapshot();
						// 生成后自动保存
						try { ideology.Set(entityId, s); } catch { }
						_ideologyText = ComposeIdeologyText(s);
					}
					catch (Exception ex) { try { Verse.Log.Warning($"[RimAI.Core][P10] Ideology.Generate failed entity={entityId}: {ex.Message}"); } catch { } }
					finally { _ideoBusy = false; }
				});
			}
			if (!_ideoBusy && Widgets.ButtonText(new Rect(rect.x + 100f, y, 90f, 28f), "RimAI.Common.Save".Translate()))
			{
				_ideoBusy = true;
				_ = Task.Run(() =>
				{
					try
					{
						var parts = ParseIdeologySegments(_ideologyText ?? string.Empty);
						var s = new RimAI.Core.Source.Modules.Persona.IdeologySnapshot { Worldview = parts.w, Values = parts.v, CodeOfConduct = parts.c, TraitsText = parts.t, UpdatedAtUtc = DateTime.UtcNow };
						ideology.Set(entityId, s);
					}
					catch (Exception ex) { try { Verse.Log.Warning($"[RimAI.Core][P10] Ideology.Save failed entity={entityId}: {ex.Message}"); } catch { } }
					finally { _ideoBusy = false; }
				});
			}
			if (_ideoBusy)
			{
				Widgets.Label(new Rect(rect.x + 200f, y + 4f, 200f, 22f), "RimAI.Common.Processing".Translate());
			}
			// 自动更新勾选
			try
			{
				var settings = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Persona.IPersonaAutoSettingsService>();
				if (_autoEnabled == null)
				{
					bool eb = settings?.GetAutoBio(entityId) ?? false;
					bool ei = settings?.GetAutoIdeo(entityId) ?? false;
					_autoEnabled = eb || ei;
				}
				bool val = _autoEnabled ?? false;
				Widgets.CheckboxLabeled(new Rect(rect.x + 300f, y, 140f, 28f), "RimAI.Common.AutoUpdate".Translate(), ref val);
				if (val != (_autoEnabled ?? false)) { settings?.SetAutoBio(entityId, val); settings?.SetAutoIdeo(entityId, val); _autoEnabled = val; }
			}
			catch { }
			y += 34f;

			// 文本区域（单一富文本框）
			var textViewH = rect.height - (y - rect.y);
			var viewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(textViewH, 200f));
			Widgets.BeginScrollView(new Rect(rect.x, y, rect.width, textViewH), ref _scrollIdeo, viewRect);
			var measure = new Rect(0f, 0f, viewRect.width - 8f, 99999f);
			float h = Mathf.Max(200f, Text.CalcHeight(_ideologyText ?? string.Empty, measure.width));
			_ideologyText = Widgets.TextArea(new Rect(4f, 4f, measure.width, h), _ideologyText ?? string.Empty);
			Widgets.EndScrollView();
		}

		private static string ComposeIdeologyText(RimAI.Core.Source.Modules.Persona.IdeologySnapshot s)
		{
			var sb = new StringBuilder();
			sb.AppendLine("RimAI.ChatUI.Ideo.WorldviewLabel".Translate()); sb.AppendLine(s?.Worldview ?? string.Empty); sb.AppendLine();
			sb.AppendLine("RimAI.ChatUI.Ideo.ValuesLabel".Translate()); sb.AppendLine(s?.Values ?? string.Empty); sb.AppendLine();
			sb.AppendLine("RimAI.ChatUI.Ideo.CodeLabel".Translate()); sb.AppendLine(s?.CodeOfConduct ?? string.Empty); sb.AppendLine();
			sb.AppendLine("RimAI.ChatUI.Ideo.TraitsLabel".Translate()); sb.AppendLine(s?.TraitsText ?? string.Empty);
			return sb.ToString().TrimEnd();
		}

		private static (string w, string v, string c, string t) ParseIdeologySegments(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) return (string.Empty, string.Empty, string.Empty, string.Empty);
			var normalized = text.Replace("\r", string.Empty);
			// 优先按空行分段（四段）
			var blocks = new List<string>();
			var sb = new StringBuilder();
			var lines = normalized.Split('\n');
			int emptyCount = 0;
			for (int i = 0; i < lines.Length; i++)
			{
				var ln = lines[i];
				if (string.IsNullOrWhiteSpace(ln))
				{
					emptyCount++;
					if (emptyCount >= 1)
					{
						if (sb.Length > 0) { blocks.Add(sb.ToString().Trim()); sb.Length = 0; }
						emptyCount = 0;
					}
					continue;
				}
				emptyCount = 0;
				// 去掉可能的段落标题
				if (ln.StartsWith("世界观") || ln.StartsWith("Worldview") || ln.StartsWith("价值观") || ln.StartsWith("Values") || ln.StartsWith("行为准则") || ln.StartsWith("Code") || ln.StartsWith("性格特质") || ln.StartsWith("Traits"))
				{
					continue;
				}
				sb.AppendLine(ln);
			}
			if (sb.Length > 0) blocks.Add(sb.ToString().Trim());
			while (blocks.Count < 4) blocks.Add(string.Empty);
			if (blocks.Count > 4) { blocks = blocks.GetRange(0, 4); }
			return (blocks[0], blocks[1], blocks[2], blocks[3]);
		}

		public void ClearCache()
		{
			_bioItems = null;
			_ideologyText = null;
			_bioGenerating = false;
			_ideoBusy = false;
			_scrollBio = Vector2.zero;
			_scrollIdeo = Vector2.zero;
		}
	}
}


