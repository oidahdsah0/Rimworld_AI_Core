using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class BuildingPowerPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public BuildingPowerPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("BuildingPowerPart requires ConfigurationService");
        }

        // 主线程立即查询：调用方需确保已在主线程
        public bool HasPoweredBuildingNow(string buildingDefName)
        {
            if (string.IsNullOrWhiteSpace(buildingDefName)) return false;
            foreach (var map in Find.Maps)
            {
                var all = map.listerBuildings?.allBuildingsColonist;
                if (all == null) continue;
                foreach (var b in all)
                {
                    if (b?.def?.defName == buildingDefName)
                    {
                        try
                        {
                            var comp = b.TryGetComp<CompPowerTrader>();
                            if (comp != null && comp.PowerOn) return true;
                        }
                        catch { }
                    }
                }
            }
            return false;
        }

        // 异步查询：自动调度到主线程
        public Task<bool> HasPoweredBuildingAsync(string buildingDefName, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => HasPoweredBuildingNow(buildingDefName), name: "BuildingPower.HasPoweredBuilding", ct: cts.Token);
        }
    }
}
