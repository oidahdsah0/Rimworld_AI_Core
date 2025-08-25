using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    // v1：扫描当前地图的蓝图与框架，估算材料缺口
    internal sealed class ConstructionBacklogPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public ConstructionBacklogPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("ConstructionBacklogPart requires ConfigurationService");
        }

        public Task<ConstructionBacklogSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap ?? throw new WorldDataException("No current map");

                // 1) 收集蓝图与框架
                var blueprints = map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).OfType<Blueprint>().ToList();
                var frames = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).OfType<Frame>().ToList();

                // 2) 资源计数器用于估算全图可用材料（只做粗略参考，不精确到可达/预留）
                var rc = map.resourceCounter;
                int GetAvailable(ThingDef def)
                {
                    try { return rc?.GetCount(def) ?? 0; } catch { return 0; }
                }

                // 3) 为每个蓝图/框架计算缺口（不考虑运输在途与专用容器，仅做只读概览）
                var items = new List<ConstructionBuildItem>();
                void AddOne(string defName, string label, List<ThingDefCountClass> cost)
                {
                    if (string.IsNullOrWhiteSpace(label)) label = defName ?? "Blueprint";
                    var missing = new List<ConstructionMissingItem>();
                    foreach (var need in cost ?? new List<ThingDefCountClass>())
                    {
                        try
                        {
                            if (need?.thingDef == null) continue;
                            // 这里不扣已投喂到该蓝图/框架的资源，旨在给出全局缺口直觉；精确缺口需逐一读取容器与在途
                            int avail = GetAvailable(need.thingDef);
                            int gap = Math.Max(0, need.count - avail);
                            if (gap > 0)
                            {
                                missing.Add(new ConstructionMissingItem { Res = need.thingDef.label ?? need.thingDef.defName, Qty = gap });
                            }
                        }
                        catch { }
                    }
                    items.Add(new ConstructionBuildItem { DefName = defName, Thing = label, Count = 1, Missing = missing });
                }

                foreach (var bp in blueprints)
                {
                    try
                    {
                        var buildable = bp?.def?.entityDefToBuild as BuildableDef;
                        var defName = buildable?.defName ?? bp?.def?.defName;
                        var label = buildable?.label ?? bp?.Label ?? bp?.def?.label;
                        var cost = bp?.TotalMaterialCost();
                        AddOne(defName, label, cost);
                    }
                    catch { }
                }
                foreach (var fr in frames)
                {
                    try
                    {
                        var buildable = fr?.def?.entityDefToBuild as BuildableDef;
                        var defName = buildable?.defName ?? fr?.def?.defName;
                        var label = buildable?.label ?? fr?.Label ?? fr?.def?.label;
                        var cost = fr?.TotalMaterialCost();
                        AddOne(defName, label, cost);
                    }
                    catch { }
                }

                // 4) 聚合同类（按 DefName/Label 归并），合并缺口按资源种类相加
                var merged = items
                    .GroupBy(i => (key: i.DefName ?? i.Thing ?? string.Empty, label: i.Thing ?? i.DefName ?? "Blueprint"))
                    .Select(g => new ConstructionBuildItem
                    {
                        DefName = g.Key.key,
                        Thing = g.Key.label,
                        Count = g.Count(),
                        Missing = MergeMissing(g.SelectMany(x => x.Missing ?? new List<ConstructionMissingItem>()))
                    })
                    .OrderByDescending(x => (x.Missing?.Sum(m => m.Qty) ?? 0))
                    .ToList();

                return new ConstructionBacklogSnapshot { Builds = merged };
            }, name: "ConstructionBacklogPart.Get", ct: cts.Token);
        }

        private static List<ConstructionMissingItem> MergeMissing(IEnumerable<ConstructionMissingItem> src)
        {
            var dic = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in src ?? Enumerable.Empty<ConstructionMissingItem>())
            {
                if (m == null) continue;
                var key = m.Res ?? string.Empty;
                if (!dic.ContainsKey(key)) dic[key] = 0;
                dic[key] += Math.Max(0, m.Qty);
            }
            return dic.Select(kv => new ConstructionMissingItem { Res = kv.Key, Qty = kv.Value }).OrderByDescending(x => x.Qty).ToList();
        }
    }
}
