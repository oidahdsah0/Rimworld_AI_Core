using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Boot
{
    // 在游戏数据加载后，运行时为目标 StatDef 注入 StatPart_RimAIServer，避免 XML Patch 解析差异
    [StaticConstructorOnStartup]
    public static class StatPartRuntimeInjector
    {
        static StatPartRuntimeInjector()
        {
            try
            {
                Inject();
            }
            catch (Exception e)
            {
                Log.Error($"[RimAI Core] StatPartRuntimeInjector failed: {e}");
            }
        }

        private static void Inject()
        {
            // 目标 StatDef 名称（包含 1.6 兼容别名）
            var targetNames = new[]
            {
                // 工作相关核心/分项
                "WorkSpeedGlobal", "GlobalWorkSpeed", "GeneralLaborSpeed",
                "ConstructionSpeed", "MiningSpeed", "PlantWorkSpeed",
                "ResearchSpeed",
                "MedicalOperationSpeed", "MedicalTendSpeed",
                // 兼容潜在别名/扩展（若存在则注入）
                "WorkSpeed", "WorkSpeedFactor", "WorkSpeedMultiplier"
            };

            var injected = new List<string>();
            foreach (var name in targetNames.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var statDef = DefDatabase<StatDef>.GetNamedSilentFail(name);
                if (statDef == null) continue; // 该环境没有此 StatDef，跳过

                if (statDef.parts == null)
                {
                    statDef.parts = new List<StatPart>();
                }

                // 避免重复添加
                bool exists = statDef.parts.Any(p => p is RimAI.Core.Source.Modules.World.Stats.StatPart_RimAIServer);
                if (exists) continue;

                try
                {
                    var part = new RimAI.Core.Source.Modules.World.Stats.StatPart_RimAIServer();
                    part.parentStat = statDef; // 关键：确保 StatPart 知道自己隶属的 StatDef
                    statDef.parts.Add(part);
                    injected.Add(name);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimAI Core] Failed to add StatPart to {name}: {ex.Message}");
                }
            }

            if (Prefs.DevMode)
            {
                var actually = DefDatabase<StatDef>.AllDefsListForReading
                    .Where(sd => sd?.parts != null && sd.parts.Any(p => p is RimAI.Core.Source.Modules.World.Stats.StatPart_RimAIServer))
                    .Select(sd => sd.defName)
                    .OrderBy(n => n)
                    .ToList();
                Log.Message($"[RimAI Core] StatPart_RimAIServer attached to: {string.Join(", ", actually)}");
            }
        }
    }
}
