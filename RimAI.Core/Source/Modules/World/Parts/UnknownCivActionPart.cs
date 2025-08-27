using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class UnknownCivActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public UnknownCivActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task DropUnknownCivGiftAsync(float quantityCoefficient = 1.0f, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Mathf.Max(timeoutMs, 3000));
            quantityCoefficient = Mathf.Max(0.1f, quantityCoefficient);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome) ?? Find.CurrentMap ?? Find.Maps?.FirstOrDefault();
                    if (map == null) return Task.CompletedTask;
                    IntVec3 spot = DropCellFinder.TradeDropSpot(map);
                    var things = ThingSetMakerDefOf.ResourcePod.root.Generate();
                    foreach (var t in things)
                    { try { t.stackCount = Mathf.Max(1, Mathf.CeilToInt(t.stackCount * quantityCoefficient)); } catch { } }
                    try { DropPodUtility.DropThingsNear(spot, map, things, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: true, forbid: true); } catch { }
                }
                catch { }
                return Task.CompletedTask;
            }, name: "World.DropUnknownCivGift", ct: cts.Token);
        }
    }
}
