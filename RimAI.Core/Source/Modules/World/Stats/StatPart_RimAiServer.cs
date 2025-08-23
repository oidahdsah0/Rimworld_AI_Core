using System;
using RimWorld;
using Verse;
using RimAI.Core.Source.Modules.World.Components;

namespace RimAI.Core.Source.Modules.World.Stats
{
    // 使用 ServerBuffCache 汇总同地图内 AI 服务器带来的全局与随机加成
    public class StatPart_RimAIServer : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            try
            {
                if (!req.HasThing) return;
                if (!(req.Thing is Pawn pawn)) return;
                var map = pawn.Map;
                var stat = parentStat;
                if (map == null || stat == null) return;

                var (globalPct, rndPct) = ServerBuffCache.GetFor(map, stat.defName);

                // 全局：作用于所有工作相关子项（不含 MoveSpeed）
                if (globalPct > 0 && IsWorkRelated(stat))
                {
                    val *= 1f + (globalPct / 100f);
                }
                // 随机：仅作用于候选子项
                if (rndPct > 0 && IsRandomCandidate(stat))
                {
                    val *= 1f + (rndPct / 100f);
                }
            }
            catch { }
        }

        public override string ExplanationPart(StatRequest req)
        {
            try
            {
                if (!req.HasThing) return null;
                if (!(req.Thing is Pawn pawn)) return null;
                var map = pawn.Map;
                var stat = parentStat;
                if (map == null || stat == null) return null;

                var (globalPct, rndPct) = ServerBuffCache.GetFor(map, stat.defName);
                System.Text.StringBuilder sb = null;
                if (globalPct > 0 && IsWorkRelated(stat))
                {
                    sb ??= new System.Text.StringBuilder();
                    sb.AppendLine($"AI服务器增益: +{globalPct}%");
                }
                if (rndPct > 0 && IsRandomCandidate(stat))
                {
                    sb ??= new System.Text.StringBuilder();
                    sb.AppendLine($"AI服务器随机增益: +{rndPct}%");
                }
                var text = sb?.ToString().Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch { return null; }
        }

        private static bool IsWorkRelated(StatDef stat)
        {
            var n = stat?.defName;
            return string.Equals(n, "WorkSpeedGlobal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "GlobalWorkSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "GeneralLaborSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "ConstructionSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MiningSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "PlantWorkSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "ResearchSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MedicalOperationSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MedicalTendSpeed", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsRandomCandidate(StatDef stat)
        {
            var n = stat?.defName;
            return string.Equals(n, "GeneralLaborSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "ConstructionSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MiningSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "PlantWorkSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "ResearchSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MedicalOperationSpeed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "MedicalTendSpeed", StringComparison.OrdinalIgnoreCase);
        }
    }
}
