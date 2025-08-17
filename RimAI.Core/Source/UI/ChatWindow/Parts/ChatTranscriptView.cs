using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class ChatTranscriptView
	{
		public static void Draw(Rect rect, RimAI.Core.Source.UI.ChatWindow.ChatConversationState state, Vector2 scrollPos, out Vector2 newScrollPos)
		{
			var prevFont = Text.Font;
			Text.Font = GameFont.Tiny; // 对话主体字体稍微调小
			// 阴影与边框背景
			Widgets.DrawShadowAround(rect);
			Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.20f));
			var bgRect = rect.ContractedBy(2f);
			Widgets.DrawWindowBackground(bgRect);
			var inner = bgRect.ContractedBy(4f);
			var contentW = inner.width - 16f; // 预留竖向滚动条宽度，避免出现水平条
			var textW = contentW - 12f;       // 文本左右内边距（6 + 6）

			// 计算整体内容高度
			float totalHeight = 0f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var msg = state.Messages[i];
				var label = FormatMessage(msg);
				var textH = Mathf.Max(24f, Text.CalcHeight(label, textW));
				totalHeight += textH + 6f; // 行间距
			}
			var viewRect = new Rect(0f, 0f, contentW, Math.Max(inner.height, totalHeight + 8f));

			Widgets.BeginScrollView(inner, ref scrollPos, viewRect);
			float cy = 0f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var msg = state.Messages[i];
				var label = FormatMessage(msg);
				var textH = Mathf.Max(24f, Text.CalcHeight(label, textW));
				var rowRect = new Rect(0f, cy, contentW, textH + 6f);
				var labelRect = new Rect(6f, cy + 3f, textW, textH);

				// 玩家消息：深蓝灰底色；AI：无底色
				if (msg.Sender == RimAI.Core.Source.UI.ChatWindow.MessageSender.User)
				{
					var deepBlueGray = new Color(0.13f, 0.17f, 0.23f, 1f); // 深蓝灰
					Widgets.DrawBoxSolid(rowRect, deepBlueGray);
					var prevColor = GUI.color;
					GUI.color = Color.white;
					Widgets.Label(labelRect, label);
					GUI.color = prevColor;
				}
				else
				{
					Widgets.Label(labelRect, label);
				}

				cy += textH + 6f;
			}
			Widgets.EndScrollView();
			// 保留用户滚动位置（不再强制置底）
			newScrollPos = scrollPos;
			Text.Font = prevFont;
		}

		private static string FormatMessage(RimAI.Core.Source.UI.ChatWindow.ChatMessage m)
		{
			var ts = m.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
			return $"[{m.DisplayName} {ts}] {m.Text}";
		}
	}
}


