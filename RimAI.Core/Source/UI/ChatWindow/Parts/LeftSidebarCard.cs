using UnityEngine;
using Verse;
using RimAI.Core.Source.UI.ChatWindow;
using RimWorld;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class LeftSidebarCard
	{
		public static void Draw(
			Rect rect,
			ref ChatTab activeTab,
			Texture2D avatarTex,
			string name,
			string jobTitle,
			ref Vector2 rosterScroll,
			System.Action onBackToChat = null,
			System.Action<Pawn> onSelectPawn = null,
			System.Func<Pawn, string> getJobTitle = null,
			bool isStreaming = false)
		{
			// 无自定义外框
			var padding = 8f;
			var x = rect.x + padding;
			var w = rect.width - padding * 2f;

			// Tabs（靠底部排列），先计算占用高度（两列布局）
			float buttonH = 28f;
			float spacing = 4f;
			// 固定 6 个正常按钮；测试按钮（DevMode）独立渲染在其上方，不影响栅格布局
			int buttonCount = 6;
			int columns = 2;
			int rows = (buttonCount + columns - 1) / columns;
			float colSpacing = 6f;
			float baseButtonsH = rows * buttonH + (rows - 1) * spacing;
			float extraTestH = Prefs.DevMode ? (buttonH + spacing) : 0f;
			float totalButtonsH = baseButtonsH + extraTestH;
			float buttonsStartY = rect.yMax - 8f - totalButtonsH; // 与上方 padding 对齐

			// 上半部分：人员名单（滚动列表）
			float headerH = 28f;
			float listTop = rect.y + 8f;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(x, listTop, w, headerH), "RimAI.ChatUI.Left.Header".Translate());
			Text.Font = GameFont.Small;
			float listY = listTop + headerH + 2f;
			var listRect = new Rect(x, listY, w, Mathf.Max(0f, buttonsStartY - listY - 8f));

			var items = new System.Collections.Generic.List<Pawn>();
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

			float rowH = 46f;
			var viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, items.Count * (rowH + 6f) + 6f));
			Widgets.BeginScrollView(listRect, ref rosterScroll, viewRect);
			float yy = 4f;
			for (int i = 0; i < items.Count; i++)
			{
				var p = items[i];
				var row = new Rect(0f, yy, viewRect.width, rowH);
				Widgets.DrawHighlightIfMouseover(row);
				// 头像
				Texture tex = null;
				try { tex = PortraitsCache.Get(p, new Vector2(rowH - 8f, rowH - 8f), Rot4.South); } catch { }
				var avatarRect = new Rect(row.x + 4f, row.y - 1f, rowH - 8f, rowH - 8f);
				if (tex != null) GUI.DrawTexture(avatarRect, tex, ScaleMode.ScaleToFit);
				// 文本：仅名称 + 下一行任职
				var textX = avatarRect.xMax + 6f;
				var textRect = new Rect(textX, row.y + 4f, Mathf.Max(0f, row.width - textX - 6f), rowH - 8f);
				string job = null; try { job = getJobTitle?.Invoke(p); } catch { job = null; }
				var line1 = (p?.LabelCap ?? "RimAI.Common.Pawn".Translate()).ToString();
				var line2 = string.IsNullOrWhiteSpace(job) ? "RimAI.ChatUI.Left.Unassigned".Translate().ToString() : job;
				var prevAnchor = Text.Anchor;
				Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small;
				Widgets.Label(textRect, line1 + "\n" + line2);
				Text.Anchor = prevAnchor;
				// 点击切换对话（流式期间禁用点击，但视觉保持）
				if (!isStreaming && Widgets.ButtonInvisible(row)) { onSelectPawn?.Invoke(p); activeTab = ChatTab.History; }
				yy += rowH + 6f;
			}
			Widgets.EndScrollView();

			// 底部 Tabs（两列靠底部排列）
			var y = buttonsStartY;
			// 先绘制（可选）Test 按钮于正常按钮之上，避免隐藏时改变下方栅格格式
			if (Prefs.DevMode)
			{
				var testRect = new Rect(x, y, w, buttonH);
				var prevEnabledT = GUI.enabled;
				GUI.enabled = !isStreaming;
				if (Widgets.ButtonText(testRect, "Test")) activeTab = ChatTab.Test;
				GUI.enabled = prevEnabledT;
				y += buttonH + spacing;
			}
			float btnW = (w - colSpacing) / 2f;
			for (int r = 0; r < rows; r++)
			{
				for (int c = 0; c < columns; c++)
				{
					int idx = r * columns + c;
					if (idx >= buttonCount) break;
					float bx = x + c * (btnW + colSpacing);
					switch (idx)
					{
						case 0:
							{
								var btnRect0 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled0 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect0, "RimAI.ChatUI.Tabs.Chat".Translate())) { onBackToChat?.Invoke(); activeTab = ChatTab.History; }
								GUI.enabled = prevEnabled0;
							}
							break;
						case 1:
							{
								var btnRect1 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled1 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect1, "RimAI.ChatUI.Tabs.Title".Translate())) activeTab = ChatTab.Title;
								GUI.enabled = prevEnabled1;
							}
							break;
						case 2:
							{
								var btnRect2 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled2 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect2, "RimAI.ChatUI.Tabs.History".Translate())) activeTab = ChatTab.HistoryAdmin;
								GUI.enabled = prevEnabled2;
							}
							break;
						case 3:
							{
								var btnRect3 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled3 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect3, "RimAI.ChatUI.Tabs.Persona".Translate())) activeTab = ChatTab.Persona;
								GUI.enabled = prevEnabled3;
							}
							break;
						case 4:
							{
								var btnRect4 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled4 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect4, "RimAI.ChatUI.Tabs.Job".Translate())) activeTab = ChatTab.Job;
								GUI.enabled = prevEnabled4;
							}
							break;
						case 5:
							{
								var btnRect5 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled5 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect5, "RimAI.ChatUI.Tabs.FixedPrompt".Translate())) activeTab = ChatTab.FixedPrompt;
								GUI.enabled = prevEnabled5;
							}
							break;
						// case 6: 已移除（Test 按钮独立渲染在顶部）
					}
				}
				y += buttonH + spacing;
			}
		}
	}
}


