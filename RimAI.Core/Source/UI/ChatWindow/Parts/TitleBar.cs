using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class TitleBar
	{
		public static void Draw(Rect rect, Texture avatarTex, string name, string jobTitle)
		{
			var padding = 6f;
			var x = rect.x + padding;
			var y = rect.y + padding;
			var h = rect.height - padding * 2f;
			// 半身像：加大尺寸并向下偏移一些
			var avatarSize = h * 1.2f;
			var avatarY = y + (h * 0.2f * 0.5f); // 轻微下移
			var avatarRect = new Rect(x, avatarY, avatarSize, avatarSize);
			if (avatarTex != null) GUI.DrawTexture(avatarRect, avatarTex, ScaleMode.ScaleAndCrop);
			x = avatarRect.xMax + 10f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Medium;
			var labelRect = new Rect(x, y, rect.width - x - 10f, h);
			var displayName = string.IsNullOrEmpty(name) ? "RimAI.Common.Pawn".Translate() : name;
			var label = string.IsNullOrEmpty(jobTitle) ? displayName : $"{displayName} , {jobTitle}";
			var prevWrap = Text.WordWrap;
			Text.WordWrap = false; // 与其他内容同行，禁止换行
			Widgets.Label(labelRect, label);
			Text.WordWrap = prevWrap;
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}


