using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    // v1 简化：基于 SlotGroup 估算占用率
    // - 对于 Zone_Stockpile：used = zone.HeldThingsCount；cap = zone.CellCount（每格至多 1 堆）
    // - 对于 Building_Storage：used = slotGroup.HeldThingsCount；cap = def.building.maxItemsInCell * def.Size.Area（支持架子等）
    // - 对于 StorageGroup：聚合成员的 CellsList 与 HeldThings 进行估算
    internal sealed class StorageSaturationPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public StorageSaturationPart(ISchedulerService scheduler, IConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("StorageSaturationPart requires ConfigurationService");
        }

        public Task<StorageSaturationSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap ?? throw new WorldDataException("No current map");
                var mgr = map.haulDestinationManager;
                var result = new List<StorageSaturationItem>();

                var groups = mgr?.AllGroupsListInPriorityOrder ?? new List<SlotGroup>();
                foreach (var sg in groups)
                {
                    if (sg == null) continue;
                    string name = SafeGetGroupName(sg);
                    float usedPct = 0f;
                    string notes = string.Empty;

                    try
                    {
                        // Zone
                        if (sg.parent is Zone_Stockpile zone)
                        {
                            int cap = Math.Max(1, zone.CellCount); // 每格至多放一堆
                            int used = Math.Max(0, zone.HeldThingsCount);
                            usedPct = cap > 0 ? Math.Min(1f, (float)used / cap) : 0f;
                            notes = $"cells={cap}, stacks={used}";
                        }
                        // Building
                        else if (sg.parent is Building_Storage bs && bs.def?.building != null)
                        {
                            int slots = Math.Max(1, bs.def.Size.Area);
                            int maxPerCell = Math.Max(1, bs.def.building.maxItemsInCell);
                            int cap = Math.Max(1, slots * maxPerCell);
                            int used = Math.Max(0, sg.HeldThingsCount);
                            usedPct = cap > 0 ? Math.Min(1f, (float)used / cap) : 0f;
                            notes = $"cells={slots}, maxPerCell={maxPerCell}, stacks={used}";
                        }
                        // StorageGroup （整组）
                        else if (sg.StorageGroup != null)
                        {
                            var g = sg.StorageGroup;
                            int capCells = Math.Max(0, g.CellsList?.Count ?? 0);
                            int used = 0;
                            try { used = g.HeldThings?.Count() ?? 0; } catch { used = 0; }
                            int cap = Math.Max(1, capCells); // 保守：每格一堆
                            usedPct = cap > 0 ? Math.Min(1f, (float)used / cap) : 0f;
                            notes = $"groupCells={capCells}, stacks={used}";
                        }
                        else
                        {
                            // 回退：使用 CellsList 与 HeldThingsCount 粗估
                            int capCells = Math.Max(0, sg.CellsList?.Count ?? 0);
                            int used = Math.Max(0, sg.HeldThingsCount);
                            int cap = Math.Max(1, capCells);
                            usedPct = cap > 0 ? Math.Min(1f, (float)used / cap) : 0f;
                            notes = $"cells={capCells}, stacks={used}";
                        }
                    }
                    catch { usedPct = 0f; }

                    bool critical = usedPct >= 0.85f;
                    result.Add(new StorageSaturationItem { Name = name, UsedPct = usedPct, Critical = critical, Notes = notes });
                }

                // 合并同名（比如多个未重命名的库存架）
                var merged = result
                    .GroupBy(x => x.Name ?? string.Empty)
                    .Select(g =>
                    {
                        // 加权平均：按条目容量近似权重（无法精确获取，临时以条目数作为权重）
                        var list = g.ToList();
                        if (list.Count == 1) return list[0];
                        float avg = list.Average(it => it.UsedPct);
                        bool crit = list.Any(it => it.Critical);
                        var sb = new StringBuilder();
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (i > 0) sb.Append(" | ");
                            sb.Append(list[i].Notes);
                        }
                        return new StorageSaturationItem { Name = g.Key, UsedPct = avg, Critical = crit, Notes = sb.ToString() };
                    })
                    .OrderByDescending(x => x.UsedPct)
                    .ToList();

                return new StorageSaturationSnapshot { Storages = merged };
            }, name: "StorageSaturationPart.Get", ct: cts.Token);
        }

        private static string SafeGetGroupName(SlotGroup sg)
        {
            try
            {
                // SlotGroup.GetName() 在反编译中存在，若不可用则回退通用标签
                if (sg?.parent is Zone_Stockpile z) return z.label ?? "Stockpile";
                if (sg?.parent is Building_Storage b) return b.Label ?? b.def?.label ?? "Storage";
                var lbl = SlotGroup.GetGroupLabel(sg);
                if (!string.IsNullOrWhiteSpace(lbl)) return lbl;
            }
            catch { }
            return "Storage";
        }
    }
}
