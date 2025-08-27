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
    internal sealed class FactionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public FactionPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("FactionPart requires ConfigurationService");
        }

        // Return eligible non-player, non-hidden, non-permanent-enemy factions that allow goodwill changes.
        public Task<IReadOnlyList<int>> GetEligibleFactionLoadIdsAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                var result = new List<int>();
                try
                {
                    var all = Find.FactionManager?.AllFactionsListForReading;
                    if (all == null) return (IReadOnlyList<int>)result;
                    foreach (var f in all)
                    {
                        if (f == null) continue;
                        if (f == Faction.OfPlayer) continue;
                        if (f.IsPlayer) continue;
                        if (f.Hidden) continue;
                        if (f.def == null) continue;
                        if (f.def.permanentEnemy) continue;
                        if (f.def.humanlikeFaction == false) continue; // prefer humanlike factions
                        try { var _ = f.loadID; } catch { continue; }
                        result.Add(f.loadID);
                    }
                }
                catch { }
                return (IReadOnlyList<int>)result;
            }, name: "FactionPart.GetEligibleFactionLoadIds", ct: cts.Token);
        }
    }
}


