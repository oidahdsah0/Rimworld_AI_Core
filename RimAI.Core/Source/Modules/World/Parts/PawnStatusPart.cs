using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;
using UnityEngine;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class PawnStatusPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public PawnStatusPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<string> GetCurrentJobLabelAsync(int pawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                try
                {
                    var report = pawn.jobs?.curDriver?.GetReport();
                    if (!string.IsNullOrWhiteSpace(report)) return report;
                    return pawn.CurJobDef != null ? (pawn.CurJobDef.label ?? pawn.CurJobDef.defName) : string.Empty;
                }
                catch { return string.Empty; }
            }, name: "GetCurrentJobLabel", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<ApparelItem>> GetApparelAsync(int pawnLoadId, int maxApparel, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var result = new System.Collections.Generic.List<ApparelItem>();
                try
                {
                    var list = pawn.apparel?.WornApparel ?? new System.Collections.Generic.List<Apparel>();
                    foreach (var a in list.Take(System.Math.Max(1, maxApparel)))
                    {
                        int maxHp = a.MaxHitPoints > 0 ? a.MaxHitPoints : 0;
                        int curHp = a.HitPoints;
                        int dp = maxHp > 0 ? Mathf.Clamp(Mathf.RoundToInt(curHp * 100f / maxHp), 0, 100) : 100;
                        string qual = string.Empty; try { if (QualityUtility.TryGetQuality(a, out var q)) qual = q.ToString(); } catch { }
                        result.Add(new ApparelItem { Label = a.LabelCap ?? a.Label, Quality = qual, DurabilityPercent = dp });
                    }
                }
                catch { }
                return (System.Collections.Generic.IReadOnlyList<ApparelItem>)result;
            }, name: "GetApparel", ct: cts.Token);
        }

        public Task<NeedsSnapshot> GetNeedsAsync(int pawnLoadId, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var needs = new NeedsSnapshot();
                try { needs.Food = pawn.needs?.food?.CurLevelPercentage ?? 0f; } catch { }
                try { needs.Rest = pawn.needs?.rest?.CurLevelPercentage ?? 0f; } catch { }
                try { needs.Recreation = pawn.needs?.joy?.CurLevelPercentage ?? 0f; } catch { }
                try { needs.Beauty = pawn.needs?.beauty?.CurLevelPercentage ?? 0f; } catch { }
                try { needs.Indoors = pawn.needs?.roomsize?.CurLevelPercentage ?? 0f; } catch { }
                try { needs.Mood = pawn.needs?.mood?.CurLevelPercentage ?? 0f; } catch { }
                return needs;
            }, name: "GetNeeds", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<ThoughtItem>> GetMoodThoughtOffsetsAsync(int pawnLoadId, int maxThoughts, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var thoughts = new System.Collections.Generic.List<ThoughtItem>();
                try
                {
                    var mem = pawn.needs?.mood?.thoughts?.memories?.Memories ?? new System.Collections.Generic.List<Thought_Memory>();
                    var top = mem
                        .Select(t => new ThoughtItem { Label = t?.LabelCap ?? t?.def?.label ?? t?.def?.defName ?? string.Empty, MoodOffset = Mathf.RoundToInt((t?.MoodOffset() ?? 0f)) })
                        .Where(x => !string.IsNullOrWhiteSpace(x.Label) && x.MoodOffset != 0)
                        .OrderBy(x => x.MoodOffset)
                        .Take(System.Math.Max(1, maxThoughts))
                        .ToList();
                    thoughts.AddRange(top);
                }
                catch { }
                return (System.Collections.Generic.IReadOnlyList<ThoughtItem>)thoughts;
            }, name: "GetMoodThoughtOffsets", ct: cts.Token);
        }
    }
}
