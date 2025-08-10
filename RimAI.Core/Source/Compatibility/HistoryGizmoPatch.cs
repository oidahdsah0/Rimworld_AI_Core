using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

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
                // 历史记录按钮
                list.Add(RimAI.Core.UI.History.HistoryShortcutGizmo.CreateForPawn(__instance));
                // 新增：闲聊 & 命令 & 任命 按钮（打开聊天/任命菜单）
                list.Add(RimAI.Core.UI.History.HistoryShortcutGizmo.CreateSmalltalkForPawn(__instance));
                list.Add(RimAI.Core.UI.History.HistoryShortcutGizmo.CreateCommandForPawn(__instance));
                list.Add(RimAI.Core.UI.History.HistoryShortcutGizmo.CreateAppointForPawn(__instance));
                __result = list;
            }
            catch { /* ignore */ }
        }
    }
}


