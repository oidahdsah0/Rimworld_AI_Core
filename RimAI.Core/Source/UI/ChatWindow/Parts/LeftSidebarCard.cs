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
			System.Func<Pawn, string> getJobTitle = null)
		{
			// 无自定义外框
			var padding = 8f;
			var x = rect.x + padding;
			var w = rect.width - padding * 2f;

			// Tabs（靠底部排列），先计算占用高度（两列布局）
			float buttonH = 28f;
			float spacing = 4f;
			int buttonCount = 6;
			int columns = 2;
			int rows = (buttonCount + columns - 1) / columns;
			float colSpacing = 6f;
			float totalButtonsH = rows * buttonH + (rows - 1) * spacing;
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
				// 点击切换对话
				if (Widgets.ButtonInvisible(row)) { onSelectPawn?.Invoke(p); activeTab = ChatTab.History; }
				yy += rowH + 6f;
			}
			Widgets.EndScrollView();

			// 底部 Tabs（两列靠底部排列）
			var y = buttonsStartY;
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
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.Chat".Translate())) { onBackToChat?.Invoke(); activeTab = ChatTab.History; }
							break;
						case 1:
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.Title".Translate())) activeTab = ChatTab.Title;
							break;
						case 2:
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.History".Translate())) activeTab = ChatTab.HistoryAdmin;
							break;
						case 3:
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.Persona".Translate())) activeTab = ChatTab.Persona;
							break;
						case 4:
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.Job".Translate())) activeTab = ChatTab.Job;
							break;
						case 5:
							if (Widgets.ButtonText(new Rect(bx, y, btnW, buttonH), "RimAI.ChatUI.Tabs.FixedPrompt".Translate())) activeTab = ChatTab.FixedPrompt;
							break;
					}
				}
				y += buttonH + spacing;
			}
		}
	}
}


