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
    internal sealed class PrisonOverviewPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public PrisonOverviewPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<PrisonOverviewSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => BuildSnapshotSafe(), name: "GetPrisonOverview", ct: cts.Token);
        }

        private PrisonOverviewSnapshot BuildSnapshotSafe()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    return new PrisonOverviewSnapshot { Count = 0, Recruitables = Array.Empty<PrisonerRecruitItem>(), Risks = Array.Empty<string>() };
                }

                var prisoners = map.mapPawns?.PrisonersOfColony?.Where(p => p != null && p.Spawned && p.guest != null).ToList() ?? new List<Pawn>();
                int count = prisoners.Count;

                var recruits = new List<PrisonerRecruitItem>();
                var risks = new List<string>();

                int lowSecureBeds = 0;
                int hungerIssues = 0;
                int angryCount = 0;

                foreach (var p in prisoners)
                {
                    try
                    {
                        var guest = p.guest;
                        // 招募相关：优先基于 ExclusiveInteractionMode；兼容转换(Convert)等非排他交互开关
                        if (guest != null && guest.Recruitable)
                        {
                            string mode = null;
                            try
                            {
                                var excl = guest.ExclusiveInteractionMode;
                                if (excl != null)
                                {
                                    // 常见：AttemptRecruit / ReduceResistance / Enslave / Convert / Execution / Release 等
                                    if (excl == PrisonerInteractionModeDefOf.AttemptRecruit) mode = "AttemptRecruit";
                                    else if (excl == PrisonerInteractionModeDefOf.ReduceResistance) mode = "ReduceResistance";
                                    else if (excl == PrisonerInteractionModeDefOf.Convert) mode = "Convert";
                                    else if (excl == PrisonerInteractionModeDefOf.Enslave) mode = "Enslave";
                                    else if (excl == PrisonerInteractionModeDefOf.Release) mode = "Release";
                                    else mode = excl.defName ?? excl.label ?? excl.ToString();
                                }
                            }
                            catch { }
                            // 非排他 Convert 开关（某些版本流程依赖该开关）
                            try { if (string.IsNullOrEmpty(mode) && guest.IsInteractionEnabled(PrisonerInteractionModeDefOf.Convert)) mode = "Convert"; } catch { }

                            if (!string.IsNullOrEmpty(mode) && !string.Equals(mode, "NoInteraction", StringComparison.OrdinalIgnoreCase))
                            {
                                recruits.Add(new PrisonerRecruitItem
                                {
                                    Pawn = p.LabelShortCap.ToString(),
                                    PawnLoadId = p.thingIDNumber,
                                    Mode = mode
                                });
                            }
                        }

                        // 风险信号：饥饿、情绪低、没有牢房（睡地/非监狱床）
                        try { if (p.needs?.food?.CurCategory == HungerCategory.Starving || p.needs?.food?.CurCategory == HungerCategory.UrgentlyHungry) hungerIssues++; } catch { }
                        try { if (p.needs?.mood != null && p.needs.mood.CurLevel <= (p.mindState?.mentalBreaker?.BreakThresholdMajor ?? 0.35f)) angryCount++; } catch { }

                        bool hasPrisonBed = false;
                        try
                        {
                            var bed = p.CurrentBed();
                            hasPrisonBed = bed != null && bed.ForPrisoners;
                        }
                        catch { }
                        if (!hasPrisonBed) lowSecureBeds++;
                    }
                    catch { }
                }

                if (lowSecureBeds > 0)
                {
                    risks.Add($"有 {lowSecureBeds} 名囚犯未在监狱床，存在管理风险");
                }
                if (hungerIssues > 0)
                {
                    risks.Add($"有 {hungerIssues} 名囚犯处于饥饿/挨饿状态");
                }
                if (angryCount > 0)
                {
                    risks.Add($"有 {angryCount} 名囚犯情绪过低，可能叛乱/逃狱");
                }

                return new PrisonOverviewSnapshot
                {
                    Count = count,
                    Recruitables = recruits,
                    Risks = risks
                };
            }
            catch (Exception ex)
            {
                try { Verse.Log.Warning($"[RimAI.Core] Prison overview failed: {ex.Message}"); } catch { }
                return new PrisonOverviewSnapshot { Count = 0, Recruitables = Array.Empty<PrisonerRecruitItem>(), Risks = new[] { "error" } };
            }
        }
    }
}
