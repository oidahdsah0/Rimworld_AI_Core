using UnityEngine;
using Verse;
using RimAI.Core.Source.UI.ChatWindow;

namespace RimAI.Core.Source.UI.ChatWindow.Parts
{
	internal static class LeftSidebarCard
	{
		public static void Draw(Rect rect, ref ChatTab activeTab, Texture2D avatarTex, string name, string jobTitle)
		{
			// 无自定义外框
			var y = rect.y + 8f;
			var padding = 8f;
			var x = rect.x + padding;
			var w = rect.width - padding * 2f;

			// 头像
			var avatarRect = new Rect(x, y, w, w * 0.6f);
			if (avatarTex != null) GUI.DrawTexture(avatarRect, avatarTex, ScaleMode.ScaleToFit);
			y = avatarRect.yMax + 6f;

			// 名称
			Text.Font = GameFont.Medium;
			var nameRect = new Rect(x, y, w, 28f);
			Widgets.Label(nameRect, name ?? "Pawn");
			y = nameRect.yMax + 2f;
			Text.Font = GameFont.Small;

			// 职务
			var jobRect = new Rect(x, y, w, 22f);
			Widgets.Label(jobRect, jobTitle ?? "无职务");
			y = jobRect.yMax + 8f;

			// Tabs
			float buttonH = 28f;
			if (Widgets.ButtonText(new Rect(x, y, w, buttonH), "历史记录")) activeTab = ChatTab.History;
			y += buttonH + 4f;
			if (Widgets.ButtonText(new Rect(x, y, w, buttonH), "人格信息")) activeTab = ChatTab.Persona;
			y += buttonH + 4f;
			if (Widgets.ButtonText(new Rect(x, y, w, buttonH), "职务管理")) activeTab = ChatTab.Job;
		}
	}
}


