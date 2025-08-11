using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using RimAI.Core.UI.History;

namespace RimAI.Core.Compatibility
{
    /// <summary>
    /// 为 Pawn 检查面板注入一个“历史记录”按钮 Gizmo。
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class HistoryGizmoPatch
    {
        static void Postfix(Pawn __instance, ref IEnumerable<Gizmo> __result)
        {
            try
            {
                if (__instance == null) return;
                // 仅对殖民地/玩家阵营小人展示
                if (__instance.Faction == null || !__instance.Faction.IsPlayer) return;
                var list = new List<Gizmo>(__result ?? System.Array.Empty<Gizmo>());
                // 仅保留一个统一入口：信息传输（打开 Chat UI，头部含 任命/历史）
                list.Add(HistoryShortcutGizmo.CreateInfoTransferForPawn(__instance));
                __result = list;
            }
            catch { /* ignore */ }
        }
    }
}


