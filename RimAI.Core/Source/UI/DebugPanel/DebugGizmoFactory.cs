using System;
using RimAI.Core.Source.Boot;
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
    }
}


