using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.Persistence;
using RimAI.Core.Source.Boot;
using RimWorld;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class JobManagerTab
	{
		public static void Draw(Rect inRect, ref Vector2 scrollRight, System.Action<Pawn> onOpenAssignDialog)
		{
			var items = new List<Pawn>();
			try
			{
				var maps = Verse.Find.Maps;
				if (maps != null)
				{
					for (int i = 0; i < maps.Count; i++)
					{
						var map = maps[i];
						if (map == null || map.mapPawns == null) continue;
						foreach (var p in map.mapPawns.FreeColonists)
						{
							if (p != null && !p.Dead) items.Add(p);
						}
					}
				}
			}
			catch { }
			float rowH = 54f;
			var viewRect = new Rect(0f, 0f, inRect.width - 16f, Mathf.Max(inRect.height, items.Count * (rowH + 6f) + 8f));
			Widgets.BeginScrollView(inRect, ref scrollRight, viewRect);
			float y = 4f;
			foreach (var p in items)
			{
				var row = new Rect(0f, y, viewRect.width, rowH);
				Widgets.DrawHighlightIfMouseover(row);
				Texture tex = null;
				try { tex = PortraitsCache.Get(p, new Vector2(rowH - 6f, rowH - 6f), Rot4.South); } catch { }
				var avatarRect = new Rect(row.x + 6f, row.y + 3f, rowH - 6f, rowH - 6f);
				if (tex != null) GUI.DrawTexture(avatarRect, tex, ScaleMode.ScaleToFit);
				float appointW = 120f;
				var appointRect = new Rect(row.xMax - 6f - appointW, row.y + 10f, appointW, rowH - 20f);
				var prevAnchor = Text.Anchor; Text.Anchor = TextAnchor.MiddleLeft; Text.Font = GameFont.Small;
				var nameX = avatarRect.xMax + 8f;
				var nameRect = new Rect(nameX, row.y, Mathf.Max(0f, appointRect.x - 8f - nameX), rowH);
				string genderTxt = string.Empty; try { genderTxt = p?.gender == Gender.Female ? "Female" : (p?.gender == Gender.Male ? "Male" : string.Empty); } catch { genderTxt = string.Empty; }
				int ageVal = 0; try { ageVal = p?.ageTracker?.AgeBiologicalYears ?? 0; } catch { ageVal = 0; }
				var partsInfo = new List<string>(); if (!string.IsNullOrWhiteSpace(genderTxt)) partsInfo.Add(genderTxt); if (ageVal > 0) partsInfo.Add(ageVal + "岁");
				var nameBase = (p?.LabelCap ?? "Pawn").ToString(); var extra = partsInfo.Count > 0 ? (" (" + string.Join(", ", partsInfo) + ")") : string.Empty;
				var jobTitle = GetJobName(p);
				var jobPart = " - " + (string.IsNullOrWhiteSpace(jobTitle) ? "未任命" : jobTitle);
				Widgets.Label(nameRect, nameBase + extra + jobPart);
				Text.Anchor = prevAnchor;
				if (Widgets.ButtonText(appointRect, "任命...")) onOpenAssignDialog?.Invoke(p);
				y += rowH + 6f;
			}
			Widgets.EndScrollView();
		}

		private static string GetJobName(Pawn pawn)
		{
			try { var persona = RimAICoreMod.Container.Resolve<IPersonaService>(); var s = persona.Get($"pawn:{pawn.thingIDNumber}")?.Job?.Name; return s ?? string.Empty; } catch { return string.Empty; }
		}

		public static void OpenAssignDialog(string entityId, string name, string desc, System.Action<string, string> onSave)
		{
			Find.WindowStack.Add(new JobAssignDialog(entityId, name, desc, onSave));
		}

		private sealed class JobAssignDialog : Window
		{
			private readonly string _entityId;
			private readonly System.Action<string, string> _onSave;
			private string _name;
			private string _desc;
			private Vector2 _scroll = Vector2.zero;

			public override Vector2 InitialSize => new Vector2(560f, 420f);

			public JobAssignDialog(string entityId, string name, string desc, System.Action<string, string> onSave)
			{
				_entityId = entityId; _name = name ?? string.Empty; _desc = desc ?? string.Empty; _onSave = onSave;
				doCloseX = true; draggable = true; absorbInputAroundWindow = true; closeOnClickedOutside = false; closeOnAccept = false; closeOnCancel = false;
			}

			public override void DoWindowContents(Rect inRect)
			{
				Text.Font = GameFont.Medium; Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "职务任命"); Text.Font = GameFont.Small;
				var nameLabel = new Rect(inRect.x, inRect.y + 34f, 60f, 28f);
				Widgets.Label(nameLabel, "职务：");
				var nameRect = new Rect(nameLabel.xMax + 6f, nameLabel.y, inRect.width - nameLabel.width - 30f, 28f);
				_name = Widgets.TextField(nameRect, _name ?? string.Empty);
				var presetLabel = new Rect(inRect.x, nameRect.yMax + 6f, 60f, 28f);
				Widgets.Label(presetLabel, "预设：");
				var presetRect = new Rect(presetLabel.xMax + 6f, presetLabel.y, inRect.width - presetLabel.width - 30f, 28f);
				DrawPresetsDropdown(presetRect);
				var descRectOuter = new Rect(inRect.x, presetRect.yMax + 6f, inRect.width, inRect.height - (presetRect.yMax + 6f) - 46f);
				var viewRect = new Rect(0f, 0f, descRectOuter.width - 16f, Mathf.Max(descRectOuter.height, Text.CalcHeight(_desc ?? string.Empty, descRectOuter.width - 16f) + 12f));
				Widgets.BeginScrollView(descRectOuter, ref _scroll, viewRect);
				_desc = Widgets.TextArea(viewRect, _desc ?? string.Empty);
				Widgets.EndScrollView();
				var bw = 90f; var sp = 8f; var br = new Rect(inRect.x, inRect.yMax - 38f, inRect.width, 32f);
				var totalW = bw * 3f + sp * 2f;
				var startX = br.xMax - totalW;
				var rSave = new Rect(startX, br.y, bw, br.height);
				var rClear = new Rect(rSave.xMax + sp, br.y, bw, br.height);
				var rExit = new Rect(rClear.xMax + sp, br.y, bw, br.height);
				if (Widgets.ButtonText(rSave, "保存")) { _onSave?.Invoke(_name?.Trim() ?? string.Empty, _desc ?? string.Empty); Close(); }
				if (Widgets.ButtonText(rClear, "清空")) { _name = string.Empty; _desc = string.Empty; }
				if (Widgets.ButtonText(rExit, "退出")) { Close(); }
				Text.Anchor = TextAnchor.UpperLeft;
			}

			private void DrawPresetsDropdown(Rect rect)
			{
				// 与原 ChatWindow.JobAssignDialog 保持一致：优先 Mod 目录，其次用户配置根
				try
				{
					var container = RimAICoreMod.Container;
					var cfg = container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>();
					var locale = (cfg as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService)?.GetInternal()?.General?.Locale ?? "zh-Hans";
					string json = null;
					try
					{
						var modRoot = RimAICoreMod.ModRootDir ?? string.Empty;
						if (!string.IsNullOrEmpty(modRoot))
						{
							var abs = System.IO.Path.Combine(modRoot, "Config", "RimAI", "Persona", $"job_presets_{locale}.json");
							if (System.IO.File.Exists(abs)) { json = System.IO.File.ReadAllText(abs); }
							else
							{
								var absZh = System.IO.Path.Combine(modRoot, "Config", "RimAI", "Persona", "job_presets_zh-Hans.json");
								if (System.IO.File.Exists(absZh)) json = System.IO.File.ReadAllText(absZh);
							}
						}
					}
					catch { }
					if (string.IsNullOrWhiteSpace(json))
					{
						var persistence = container.Resolve<IPersistenceService>();
						var rel = $"RimAI/Persona/job_presets_{locale}.json";
						json = persistence.ReadTextUnderConfigOrNullAsync(rel).GetAwaiter().GetResult();
						if (string.IsNullOrWhiteSpace(json)) { rel = "RimAI/Persona/job_presets_zh-Hans.json"; json = persistence.ReadTextUnderConfigOrNullAsync(rel).GetAwaiter().GetResult(); }
					}
					if (!string.IsNullOrWhiteSpace(json))
					{
						var arr = Newtonsoft.Json.Linq.JArray.Parse(json);
						var floatMenu = new List<FloatMenuOption>();
						for (int i = 0; i < arr.Count; i++)
						{
							var n = arr[i][(object)"name"]?.ToString() ?? string.Empty;
							var d = arr[i][(object)"description"]?.ToString() ?? string.Empty;
							var idxName = n;
							floatMenu.Add(new FloatMenuOption(idxName, () => { _name = n; _desc = d; }));
						}
						if (Widgets.ButtonText(rect, "(选择预设)")) { if (floatMenu.Count > 0) Find.WindowStack.Add(new FloatMenu(floatMenu)); }
					}
				}
				catch { }
			}
		}
	}
}


