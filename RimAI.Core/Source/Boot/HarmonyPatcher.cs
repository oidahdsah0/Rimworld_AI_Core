using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.UI.DebugPanel;
using Verse;

namespace RimAI.Core.Source.Boot
{
    internal static class HarmonyPatcher
    {
        public static void Apply(Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos));
                var postfix = new HarmonyMethod(typeof(Pawn_GetGizmos_Patch), nameof(Pawn_GetGizmos_Patch.Postfix));
                harmony.Patch(method, postfix: postfix);
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Core][P1] Harmony patch failed: {ex}");
                throw;
            }
        }
    }

    [HarmonyPatch]
    internal static class Pawn_GetGizmos_Patch
    {
        public static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            try
            {
                // Only show in developer mode and when DebugPanel is enabled
                if (!Prefs.DevMode) return;

                var cfgService = RimAICoreMod.Container.Resolve<IConfigurationService>();
                if (cfgService?.Current?.DebugPanelEnabled != true) return;

                // Append our debug button at the end of gizmos
                var list = __result?.ToList() ?? new List<Gizmo>();
                list.Add(DebugGizmoFactory.CreateOpenDebugWindowGizmo());
                __result = list;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Core][P1] Postfix Pawn.GetGizmos failed: {ex}");
            }
        }
    }
}


