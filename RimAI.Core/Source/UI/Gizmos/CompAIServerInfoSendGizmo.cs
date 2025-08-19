using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

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
                defaultLabel = "RimAI.ChatUI.Gizmo.Open".Translate(),
                defaultDesc = "RimAI.ChatUI.Gizmo.OpenDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get("RimAI/Chat/InfoSend", true),
                action = () => { /* 暂不执行任何操作（机器有独立 UI，后续接入） */ }
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


