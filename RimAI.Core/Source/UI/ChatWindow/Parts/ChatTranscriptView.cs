using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class ChatTranscriptView
	{
		public static void Draw(Rect rect, RimAI.Core.Source.UI.ChatWindow.ChatConversationState state, Vector2 scrollPos, out Vector2 newScrollPos)
		{
			var inner = rect;
			var contentW = inner.width - 16f;

			// 计算高度
			float y = 0f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var msg = state.Messages[i];
				var label = FormatMessage(msg);
				y += Mathf.Max(24f, Text.CalcHeight(label, contentW)) + 6f;
			}
			var viewRect = new Rect(0f, 0f, contentW, Math.Max(inner.height, y + 8f));

			Widgets.BeginScrollView(inner, ref scrollPos, viewRect);
			float cy = 0f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var msg = state.Messages[i];
				var label = FormatMessage(msg);
				var h = Mathf.Max(24f, Text.CalcHeight(label, contentW));
				var rowRect = new Rect(0f, cy, contentW, h);
				Widgets.Label(rowRect, label);
				cy += h + 6f;
			}
			Widgets.EndScrollView();
			newScrollPos = new Vector2(0f, viewRect.height);
		}

		private static string FormatMessage(RimAI.Core.Source.UI.ChatWindow.ChatMessage m)
		{
			var ts = m.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
			return $"[{m.DisplayName} {ts}] {m.Text}";
		}
	}
}


