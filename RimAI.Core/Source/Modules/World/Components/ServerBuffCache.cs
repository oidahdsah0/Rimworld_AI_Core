using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Components
{
    internal static class ServerBuffCache
    {
        private sealed class CacheEntry
        {
            public int LastTick;
            public int GlobalPercent;
            public Dictionary<string, int> RandomTotalsByStat = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        private static readonly Dictionary<Map, CacheEntry> _cache = new Dictionary<Map, CacheEntry>();
        private const int CacheIntervalTicks = 300; // 与原巡检一致，约 5 秒

        private static readonly string[] ServerDefNames = new[]
        {
            "RimAI_AIServer_Lv1A", "RimAI_AIServer_Lv2A", "RimAI_AIServer_Lv3A"
        };

        private static readonly List<string> RandomCandidateNames = new List<string>
        {
            "GeneralLaborSpeed", "ConstructionSpeed", "MiningSpeed",
            "PlantWorkSpeed", "ResearchSpeed", "MedicalOperationSpeed", "MedicalTendSpeed"
        };

        public static (int globalPercent, int randomPercentForStat) GetFor(Map map, string statDefName)
        {
            if (map == null || string.IsNullOrEmpty(statDefName)) return (0, 0);
            var tick = Find.TickManager?.TicksGame ?? 0;

            if (!_cache.TryGetValue(map, out var entry))
            {
                entry = new CacheEntry();
                _cache[map] = entry;
            }

            if (tick - entry.LastTick >= CacheIntervalTicks)
            {
                Rebuild(map, entry);
                entry.LastTick = tick;
            }

            entry.RandomTotalsByStat.TryGetValue(statDefName, out var rnd);
            return (entry.GlobalPercent, rnd);
        }

        private static void Rebuild(Map map, CacheEntry entry)
        {
            try
            {
                int global = 0;
                var rndTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                // 分 Def 获取，避免 AllThings 全表扫描
                IEnumerable<Thing> EnumerateServers()
                {
                    foreach (var defName in ServerDefNames)
                    {
                        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                        if (def == null) continue;
                        var list = map.listerThings?.ThingsOfDef(def) ?? new List<Thing>();
                        foreach (var t in list) yield return t;
                    }
                }

                foreach (var t in EnumerateServers())
                {
                    try
                    {
                        if (t == null) continue;
                        if (t is Building b)
                        {
                            var powered = b.GetComp<CompPowerTrader>()?.PowerOn ?? false;
                            var flick = b.GetComp<CompFlickable>();
                            bool switchedOn = flick == null || flick.SwitchIsOn;
                            var broken = b.GetComp<CompBreakdownable>()?.BrokenDown ?? false;
                            if (!powered || !switchedOn || broken) continue;
                        }

                        var comp = t.TryGetComp<RimAI.Core.Source.Modules.World.Comps.Comp_AiServerBuffs>();
                        var props = comp?.Props;
                        if (props == null) continue;

                        global += Math.Max(0, props.baseGlobalWorkSpeedPercent);

                        int k = Math.Max(0, props.randomAttributeCount);
                        int pct = Math.Max(0, props.randomAttributePercent);
                        if (k <= 0 || pct <= 0) continue;

                        int seed = unchecked((t.thingIDNumber * 397) ^ (t.Position.GetHashCode() * 17) ^ (t.def?.defName?.GetHashCode() ?? 0));
                        var rng = new System.Random(seed);
                        var shuffled = RandomCandidateNames.OrderBy(_ => rng.Next()).ToList();
                        foreach (var name in shuffled.Take(Math.Min(k, RandomCandidateNames.Count)))
                        {
                            rndTotals.TryGetValue(name, out var cur);
                            rndTotals[name] = cur + pct;
                        }
                    }
                    catch { }
                }

                entry.GlobalPercent = Math.Max(0, global);
                entry.RandomTotalsByStat = rndTotals;
            }
            catch { }
        }
    }
}
