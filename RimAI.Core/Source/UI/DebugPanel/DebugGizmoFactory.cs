using System;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.UI.ChatWindow.Strings;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel
{
    internal static class DebugGizmoFactory
    {
        public static Command_Action CreateOpenDebugWindowGizmo()
        {
            var cmd = new Command_Action
            {
                defaultLabel = "RimAI Debug",
                defaultDesc = "Open RimAI Core debug window (P1/P2).",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", true),
                action = () => Find.WindowStack.Add(new DebugWindow())
            };
            return cmd;
        }

		public static Command_Action CreateOpenChatWindowGizmo(Pawn pawn)
		{
			var cmd = new Command_Action
			{
				defaultLabel = Keys.OpenChatWindow,
				defaultDesc = "打开信息传输窗口（玩家与小人对话）",
				icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower", true),
				action = () =>
				{
					if (pawn == null) return;
					Find.WindowStack.Add(new RimAI.Core.Source.UI.ChatWindow.ChatWindow(pawn));
				}
			};
			return cmd;
		}
    }
}


