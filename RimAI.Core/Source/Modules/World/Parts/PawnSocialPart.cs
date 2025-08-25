using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class PawnSocialPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public PawnSocialPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<PawnSocialSnapshot> GetPawnSocialSnapshotAsync(int pawnLoadId, int topRelations, int recentSocialEvents, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null;
                foreach (var map in Find.Maps)
                {
                    foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>())
                    { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } }
                    if (pawn != null) break;
                }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var relations = new System.Collections.Generic.List<SocialRelationItem>();
                try
                {
                    var rels = pawn.relations?.DirectRelations ?? new System.Collections.Generic.List<DirectPawnRelation>();
                    foreach (var r in rels)
                    {
                        var other = r.otherPawn; if (other == null) continue;
                        relations.Add(new SocialRelationItem
                        {
                            RelationKind = r.def?.label ?? r.def?.defName ?? string.Empty,
                            OtherName = other.Name?.ToStringShort ?? other.LabelCap ?? "Pawn",
                            OtherEntityId = $"pawn:{other.thingIDNumber}",
                            Opinion = pawn.relations?.OpinionOf(other) ?? 0
                        });
                    }
                }
                catch { }
                var ordered = relations.OrderByDescending(x => x.Opinion).Take(UnityEngine.Mathf.Max(0, topRelations)).ToList();
                var eventsList = new System.Collections.Generic.List<SocialEventItem>();
                try
                {
                    var events = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetRecentSocialEvents(pawn, recentSocialEvents) ?? new System.Collections.Generic.List<SocialEventItem>();
                    eventsList.AddRange(events);
                }
                catch { }
                return new PawnSocialSnapshot { Relations = ordered, RecentEvents = eventsList };
            }, name: "GetPawnSocialSnapshot", ct: cts.Token);
        }
    }
}
