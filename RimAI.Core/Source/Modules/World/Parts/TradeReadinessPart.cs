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
    internal sealed class TradeReadinessPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public TradeReadinessPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("TradeReadinessPart requires ConfigurationService");
        }

        public Task<TradeReadinessSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => BuildSnapshot(), name: "GetTradeReadiness", ct: cts.Token);
        }

        private TradeReadinessSnapshot BuildSnapshot()
        {
            if (Current.Game == null) throw new WorldDataException("World not loaded");
            var map = Find.CurrentMap ?? throw new WorldDataException("No current map");

            // 银币（仅统计信标覆盖内，用于交易可用）
            int silver = 0;
            try
            {
                silver = TradeUtility.AllLaunchableThingsForTrade(map).Where(t => t.def == ThingDefOf.Silver).Sum(t => t.stackCount);
            }
            catch { silver = 0; }

            // 信标状态与覆盖
            int beaconsTotal = 0, beaconsPowered = 0, coverageCells = 0, inRangeStacks = 0;
            try
            {
                var allBeacons = map.listerBuildings.allBuildingsColonist.OfType<Building_OrbitalTradeBeacon>().ToList();
                beaconsTotal = allBeacons.Count;
                var powered = Building_OrbitalTradeBeacon.AllPowered(map).ToList();
                beaconsPowered = powered.Count;
                var covered = new HashSet<IntVec3>();
                foreach (var b in powered)
                {
                    foreach (var c in b.TradeableCells) covered.Add(c);
                }
                coverageCells = covered.Count;
                foreach (var cell in covered)
                {
                    var list = cell.GetThingList(map);
                    for (int i = 0; i < list.Count; i++)
                    {
                        var t = list[i];
                        if (t.def.category == ThingCategory.Item) inRangeStacks++;
                    }
                }
            }
            catch { }

            // 通讯台可用性（任意一个可用）
            bool hasConsole = false, usableNow = false;
            try
            {
                foreach (var b in map.listerBuildings.allBuildingsColonist)
                {
                    if (b is Building_CommsConsole cc)
                    {
                        hasConsole = true;
                        try { if (cc.CanUseCommsNow) usableNow = true; } catch { }
                    }
                }
            }
            catch { }

            // 可交易物资（信标覆盖范围；去重按 def 合并数量和值）
            var goods = new List<TradeGoodsItem>();
            try
            {
                var grouped = TradeUtility.AllLaunchableThingsForTrade(map)
                    .Where(t => TradeUtility.PlayerSellableNow(t, null))
                    .GroupBy(t => t.def);
                foreach (var g in grouped)
                {
                    var def = g.Key; if (def == null) continue;
                    string label = def.label ?? def.defName;
                    int qty = 0; float totalValue = 0f;
                    foreach (var t in g)
                    {
                        int c = 0; try { c = t.stackCount; } catch { c = 0; }
                        qty += c;
                        float mv = 0f; try { mv = t.MarketValue; } catch { mv = 0f; }
                        totalValue += mv * c;
                    }
                    if (qty > 0)
                    {
                        goods.Add(new TradeGoodsItem { DefName = def.defName, Label = label, Qty = qty, TotalValue = totalValue });
                    }
                }
                goods = goods.OrderByDescending(x => x.TotalValue).ThenByDescending(x => x.Qty).ToList();
            }
            catch { }

            return new TradeReadinessSnapshot
            {
                Silver = silver,
                Beacons = new TradeBeaconInfo { Total = beaconsTotal, Powered = beaconsPowered, CoverageCells = coverageCells, InRangeStacks = inRangeStacks },
                Comms = new TradeCommsInfo { HasConsole = hasConsole, UsableNow = usableNow },
                Goods = goods
            };
        }
    }
}
