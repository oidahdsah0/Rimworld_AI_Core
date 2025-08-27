using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class PartyActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public PartyActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler; _cfg = cfg;
        }

        // 改为仅通过“强制移动”实现：让同地图的部分殖民者移动到发起者附近；若无任何移动成功则视为失败。
        public Task<bool> TryStartPartyAsync(int initiatorPawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (Current.Game == null) return false;
                    Pawn initiator = null; Map map = null;
                    foreach (var m in Find.Maps)
                    {
                        foreach (var p in m.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>())
                        {
                            if (p?.thingIDNumber == initiatorPawnLoadId) { initiator = p; map = m; break; }
                        }
                        if (initiator != null) break;
                    }
                    if (initiator == null || map == null) return false;

                    var center = initiator.Position;
                    var radius = 10; // 简单固定半径
                    var maxCount = 8; // 最多强制移动 8 名

                    // 选择候选：同地图、玩家派系、人形、未倒地、已生成、不是发起者
                    var candidates = (map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>())
                        .Where(p => p != null && p != initiator && p.Faction == Faction.OfPlayer)
                        .Where(p => p.RaceProps?.Humanlike == true)
                        .Where(p => !p.Downed && p.Spawned)
                        .Take(maxCount)
                        .ToList();

                    bool anyIssued = false;
                    foreach (var pawn in candidates)
                    {
                        try
                        {
                            var dest = CellFinder.RandomClosewalkCellNear(center, map, radius);
                            try { if (pawn.drafter != null) pawn.drafter.Drafted = false; } catch { }
                            pawn.jobs?.StartJob(new Job(JobDefOf.Goto, dest), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
                            pawn.jobs?.jobQueue?.EnqueueLast(new Job(JobDefOf.Wait));
                            anyIssued = true;
                        }
                        catch { }
                    }

                    return anyIssued;
                }
                catch { return false; }
            }, name: "World.TryStartParty", ct: cts.Token);
        }
    }
}
