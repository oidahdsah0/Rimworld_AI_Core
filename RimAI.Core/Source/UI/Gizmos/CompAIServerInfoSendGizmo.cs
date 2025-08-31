using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
// avoid namespace/type ambiguity; use fully qualified name when constructing window

namespace RimAI.Core.Source.UI.Gizmos
{
    internal sealed class CompAIServerInfoSendGizmo : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (var g in base.CompGetGizmosExtra()) yield return g;

            // 仅玩家阵营且通电时可见
            if (parent == null || parent.Faction != Faction.OfPlayer) yield break;
            var power = parent.TryGetComp<CompPowerTrader>();
            if (power == null || !power.PowerOn) yield break;

            yield return new Command_Action
            {
                // 复用 ChatWindow 的打开键：“信息传输”
                defaultLabel = "RimAI.ChatUI.Gizmo.Open".Translate(),
                defaultDesc = "RimAI.Core.Buildings.InfoSendDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("RimAI/Chat/InfoSend", true),
                action = () =>
                {
                    try
                    {
                        var id = parent?.thingIDNumber ?? 0;
                        var entityId = $"thing:{id}";
                        // 打开 ServerChatWindow 针对该服务器的会话（让窗口在后台解析真实等级并更新记录）
                        Find.WindowStack.Add(new RimAI.Core.Source.UI.ServerChatWindow.ServerChatWindow(entityId));
                    }
                    catch { }
                }
            };
        }
    }

    internal sealed class CompProperties_AIServerInfoSendGizmo : CompProperties
    {
        public CompProperties_AIServerInfoSendGizmo()
        {
            compClass = typeof(CompAIServerInfoSendGizmo);
        }
    }
}


