using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.UI.ChatWindow;
using UnityEngine;
using RimWorld;
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

				// Ensure SchedulerGameComponent is present when game finishes init
				var gameFinalize = AccessTools.Method(typeof(Game), nameof(Game.FinalizeInit));
				var gamePostfix = new HarmonyMethod(typeof(Game_FinalizeInit_Patch), nameof(Game_FinalizeInit_Patch.Postfix));
				harmony.Patch(gameFinalize, postfix: gamePostfix);

                // 移除旧的 MapComponent 绑定（缓存方案无需）

                // (Removed) Inspect string postfix for AI buff line
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
                var list = __result?.ToList() ?? new List<Gizmo>();

                // 常规入口：信息传输（仅我方殖民地居民拥有）
                try
                {
                    if (__instance != null && __instance.Faction == Faction.OfPlayer && __instance.IsColonist)
                    {
                        var cmd = new Command_Action
                        {
                            defaultLabel = "RimAI.ChatUI.Gizmo.Open".Translate(),
                            defaultDesc = "RimAI.ChatUI.Gizmo.OpenDesc".Translate(),
                            icon = ContentFinder<Texture2D>.Get("RimAI/Chat/InfoSend", true),
                            action = () =>
                            {
                                try
                                {
                                    // Verse.Log.Message($"[RimAI.Core][P10] Gizmo click ChatWindow for pawn={__instance?.thingIDNumber}");
                                    if (__instance != null) Find.WindowStack.Add(new ChatWindow(__instance));
                                }
                                catch (Exception ex)
                                {
                                    Verse.Log.Error($"[RimAI.Core][P10] Gizmo open ChatWindow failed: {ex}");
                                }
                            }
                        };
                        list.Add(cmd);

                        // Dev-only dump gizmo removed to reduce clutter; use logs if needed.
                    }
                }
                catch { }

                __result = list;
            }
            catch (Exception ex)
            {
                Log.Error($"[RimAI.Core][P1] Postfix Pawn.GetGizmos failed: {ex}");
            }
        }
    }
}

	[HarmonyPatch]
	internal static class Game_FinalizeInit_Patch
	{
		public static void Postfix(Game __instance)
		{
			try
			{
				if (__instance == null) return;
				// Ensure SchedulerGameComponent is created (idempotent)
				__instance.GetComponent<RimAI.Core.Source.Infrastructure.Scheduler.SchedulerGameComponent>();

				// Ensure PersistenceManager is created (idempotent)
				__instance.GetComponent<RimAI.Core.Source.Modules.Persistence.PersistenceManager>();
			}
			catch (Exception ex)
			{
				Log.Error($"[RimAI.Core][P3] Ensure SchedulerGameComponent failed: {ex}");
			}
		}
	}

    // Map_FinalizeInit_Patch removed


    // (Removed) Pawn_GetInspectString_Patch


