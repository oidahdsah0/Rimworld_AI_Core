using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerLeftSidebarCard
	{
		public static void Draw(
			Rect rect,
			ref ServerTab activeTab,
			string headerName,
			string subTitle,
			ref Vector2 rosterScroll,
			System.Action onBackToChat = null,
			System.Action<ServerListItem> onSelectServer = null,
			IReadOnlyList<ServerListItem> items = null,
			bool isStreaming = false,
			System.Func<ServerListItem, Texture> getIcon = null)
		{
			// 与 ChatWindow 左栏保持一致的布局与体验，但实现独立，方便后续改造
			var padding = 8f;
			var x = rect.x + padding;
			var w = rect.width - padding * 2f;

			float buttonH = 28f;
			float spacing = 4f;
			int buttonCount = 4;   // 下面的网格按钮数量（对话/人格/AI Log/历史）
			int columns = 2;
			int rows = (buttonCount + columns - 1) / columns;
			float colSpacing = 6f;
			// 额外在网格按钮区域顶部添加一个“工具管理”按钮（独占整行）
			bool hasToolManagerButton = true;
			float extraTopButtonH = hasToolManagerButton ? (buttonH + spacing) : 0f;
			float totalButtonsH = extraTopButtonH + rows * buttonH + (rows - 1) * spacing;
			float buttonsStartY = rect.yMax - 8f - totalButtonsH;

			float headerH = 28f;
			float listTop = rect.y + 8f;
			Text.Font = GameFont.Medium;
			Widgets.Label(new Rect(x, listTop, w, headerH), headerName ?? "RimAI.SCW.Left.Header".Translate());
			Text.Font = GameFont.Small;
			float listY = listTop + headerH + 2f;
			var listRect = new Rect(x, listY, w, Mathf.Max(0f, buttonsStartY - listY - 8f));

			items = items ?? System.Array.Empty<ServerListItem>();

			float rowH = 46f;
			var viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, items.Count * (rowH + 6f) + 6f));
			Widgets.BeginScrollView(listRect, ref rosterScroll, viewRect);
			float yy = 4f;
			for (int i = 0; i < items.Count; i++)
			{
				var s = items[i];
				var row = new Rect(0f, yy, viewRect.width, rowH);
				Widgets.DrawHighlightIfMouseover(row);
				Texture tex = null;
				try { tex = getIcon?.Invoke(s); } catch { }
				var avatarRect = new Rect(row.x + 4f, row.y - 1f + 5f, rowH - 8f, rowH - 8f);
				if (tex != null) GUI.DrawTexture(avatarRect, tex, ScaleMode.ScaleToFit);
				var textX = avatarRect.xMax + 6f;
				var textRect = new Rect(textX, row.y + 4f, Mathf.Max(0f, row.width - textX - 6f), rowH - 8f);
				var line1 = s.DisplayName ?? "AI Server";
				var line2 = $"ID:{s.ThingId}  LV:{s.Level}";
				var prevAnchor = Text.Anchor;
				Text.Anchor = TextAnchor.UpperLeft; Text.Font = GameFont.Small;
				Widgets.Label(textRect, line1 + "\n" + line2);
				Text.Anchor = prevAnchor;
				if (!isStreaming && Widgets.ButtonInvisible(row)) { onSelectServer?.Invoke(s); activeTab = ServerTab.Chat; }
				if (!isStreaming && Widgets.ButtonInvisible(avatarRect)) { onSelectServer?.Invoke(s); activeTab = ServerTab.Chat; }
				yy += rowH + 6f;
			}
			Widgets.EndScrollView();

			var y = buttonsStartY;
			// 顶部“工具管理”按钮（跨两列）
			if (hasToolManagerButton)
			{
				var tmRect = new Rect(x, y, w, buttonH);
				var prev = GUI.enabled;
				GUI.enabled = !isStreaming;
				if (Widgets.ButtonText(tmRect, "RimAI.SCW.Tools.ManagerButton".Translate()))
				{
					activeTab = ServerTab.Tools;
				}
				GUI.enabled = prev;
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
								if (Widgets.ButtonText(btnRect0, "RimAI.SCW.Tabs.Chat".Translate())) { onBackToChat?.Invoke(); activeTab = ServerTab.Chat; }
								GUI.enabled = prevEnabled0;
							}
							break;
						case 1:
							{
								var btnRect1 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled1 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect1, "RimAI.SCW.Tabs.Persona".Translate())) activeTab = ServerTab.Persona;
								GUI.enabled = prevEnabled1;
							}
							break;
						case 2:
							{
								var btnRect2 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled2 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect2, "RimAI.SCW.Tabs.AiLog".Translate())) activeTab = ServerTab.AiLog;
								GUI.enabled = prevEnabled2;
							}
							break;
						case 3:
							{
								var btnRect3 = new Rect(bx, y, btnW, buttonH);
								var prevEnabled3 = GUI.enabled;
								GUI.enabled = !isStreaming;
								if (Widgets.ButtonText(btnRect3, "RimAI.SCW.Tabs.History".Translate())) activeTab = ServerTab.History;
								GUI.enabled = prevEnabled3;
							}
							break;
					}
				}
				y += buttonH + spacing;
			}
		}
	}
}
