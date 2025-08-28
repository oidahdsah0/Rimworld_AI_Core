using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class FactionGoodwillAdjustResult
    {
        public int FactionId { get; set; }
        public string FactionName { get; set; }
        public string FactionDefName { get; set; }
        public int Before { get; set; }
        public int After { get; set; }
    }

    internal sealed class FactionActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public FactionActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("FactionActionPart requires ConfigurationService");
        }

        public Task<FactionGoodwillAdjustResult> TryAdjustGoodwillAsync(int factionLoadId, int delta, string reason, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    var target = TryFindFactionByLoadId(factionLoadId);
                    if (target == null) return null;
                    var player = Faction.OfPlayer;
                    if (player == null) return null;

                    // RimWorld goodwill is typically in -100..100
                    int before = player.RelationKindWith(target) != FactionRelationKind.Hostile ? (int)Math.Round((double)player.GoodwillWith(target)) : -100;
                    // Use RimWorld API to change goodwill; if not available directly, manipulate relations
                    // Prefer GoodwillUtility if present, otherwise adjust through SetGoodwill or TryAffectGoodwill
                    try
                    {
                        // 在较新版本 API 下，直接设置 Goodwill 前，先确保不是敌对关系
                        try { Faction.OfPlayer.RelationKindWith(target); } catch { }
                    }
                    catch { }

                    try
                    {
                        Faction.OfPlayer.TryAffectGoodwillWith(target, delta, canSendMessage: false, canSendHostilityLetter: false, HistoryEventDefOf.DebugGoodwill);
                    }
                    catch { return null; }

                    int after = player.RelationKindWith(target) != FactionRelationKind.Hostile ? (int)Math.Round((double)player.GoodwillWith(target)) : -100;

                    return new FactionGoodwillAdjustResult
                    {
                        FactionId = target.loadID,
                        FactionName = target.Name ?? target.def?.label ?? target.def?.defName ?? "faction",
                        FactionDefName = target.def?.defName ?? "",
                        Before = before,
                        After = after
                    };
                }
                catch
                {
                    return null;
                }
            }, name: "FactionAction.TryAdjustGoodwill", ct: cts.Token);
        }

        private static Faction TryFindFactionByLoadId(int loadId)
        {
            try
            {
                foreach (var f in Find.FactionManager?.AllFactionsListForReading ?? new System.Collections.Generic.List<Faction>())
                {
                    if (f != null && f.loadID == loadId) return f;
                }
            }
            catch { }
            return null;
        }
    }
}


