using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class TitleBar
	{
		public static void Draw(Rect rect, Texture2D avatarTex, string name, string jobTitle)
		{
			var padding = 6f;
			var x = rect.x + padding;
			var y = rect.y + padding;
			var h = rect.height - padding * 2f;
			var avatarRect = new Rect(x, y, h, h);
			if (avatarTex != null) GUI.DrawTexture(avatarRect, avatarTex, ScaleMode.ScaleToFit);
			x = avatarRect.xMax + 8f;
			Text.Anchor = TextAnchor.MiddleLeft;
			Text.Font = GameFont.Medium;
			var nameRect = new Rect(x, y, rect.width - x - 10f, h * 0.6f);
			Widgets.Label(nameRect, name ?? "Pawn");
			Text.Font = GameFont.Small;
			var jobRect = new Rect(x, nameRect.yMax, rect.width - x - 10f, h * 0.4f);
			Widgets.Label(jobRect, jobTitle ?? "无职务");
			Text.Anchor = TextAnchor.UpperLeft;
		}
	}
}


