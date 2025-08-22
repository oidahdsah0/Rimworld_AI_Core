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
                defaultLabel = "RimAI.ServerChatUI.Button.BackToChat".Translate(),
                defaultDesc = "RimAI.Core.Buildings.InfoSendDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("RimAI/Chat/InfoSend", true),
                action = () =>
                {
                    try
                    {
                        var id = parent?.thingIDNumber ?? 0;
                        var entityId = $"thing:{id}";
                        // 初始化服务器记录，以便周期任务与提示词使用
                        try { var server = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>(); server?.GetOrCreate(entityId, 1); } catch { }
                        // 打开 ServerChatWindow 针对该服务器的会话
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


