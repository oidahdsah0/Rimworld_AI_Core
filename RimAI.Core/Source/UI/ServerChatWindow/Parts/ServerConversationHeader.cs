using UnityEngine;
using Verse;
using RimAI.Core.Source.UI.ChatWindow.Parts;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
	internal static class ServerConversationHeader
	{
		public static void Draw(Rect titleRect, Texture icon, string serverName, string subLine)
		{
			// 复用 TitleBar 风格：左侧图标 + 两行文本；右侧暂留空（后续可放状态/工具）
			TitleBar.Draw(titleRect, icon, serverName ?? "AI Server", subLine ?? string.Empty);
		}
	}
}
