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
    internal sealed class PawnHealthPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public PawnHealthPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<PawnHealthSnapshot> GetPawnHealthSnapshotAsync(int pawnLoadId, CancellationToken ct = default)
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
                    foreach (var p in map.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                    { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } }
                    if (pawn != null) break;
                }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var dead = pawn.Dead;
                float Lv(PawnCapacityDef def) { try { return Mathf.Clamp01(pawn.health.capacities.GetLevel(def)); } catch { return 0f; } }
                var eatingDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Eating");
                var snap = new PawnHealthSnapshot
                {
                    PawnLoadId = pawnLoadId,
                    Consciousness = Lv(PawnCapacityDefOf.Consciousness),
                    Moving = Lv(PawnCapacityDefOf.Moving),
                    Manipulation = Lv(PawnCapacityDefOf.Manipulation),
                    Sight = Lv(PawnCapacityDefOf.Sight),
                    Hearing = Lv(PawnCapacityDefOf.Hearing),
                    Talking = Lv(PawnCapacityDefOf.Talking),
                    Breathing = Lv(PawnCapacityDefOf.Breathing),
                    BloodPumping = Lv(PawnCapacityDefOf.BloodPumping),
                    BloodFiltration = Lv(PawnCapacityDefOf.BloodFiltration),
                    Metabolism = eatingDef != null ? Lv(eatingDef) : 0f,
                    IsDead = dead,
                    AveragePercent = 0f
                };
                try
                {
                    var list = new System.Collections.Generic.List<HediffItem>();
                    var hediffs = pawn.health?.hediffSet?.hediffs ?? new System.Collections.Generic.List<Hediff>();
                    foreach (var hdf in hediffs)
                    {
                        if (hdf == null) continue;
                        var label = hdf.LabelBaseCap ?? hdf.LabelCap ?? hdf.def?.label ?? hdf.def?.defName ?? string.Empty;
                        var part = hdf.Part?.LabelCap ?? string.Empty;
                        var sev = 0f; try { sev = hdf.Severity; } catch { sev = 0f; }
                        bool perm = false; try { perm = hdf.IsPermanent(); } catch { perm = false; }
                        string cat = "Other";
                        try
                        {
                            if (hdf is Hediff_MissingPart) cat = "MissingPart";
                            else if (hdf is Hediff_AddedPart) cat = "Implant";
                            else if (hdf.def?.injuryProps != null) cat = "Injury";
                            else if (hdf.def?.isBad == true) cat = "Disease";
                        }
                        catch { }
                        list.Add(new HediffItem { Label = label, Part = part, Severity = sev, Permanent = perm, Category = cat });
                    }
                    snap.Hediffs = list;
                }
                catch { }
                return snap;
            }, name: "GetPawnHealthSnapshot", ct: cts.Token);
        }
    }
}
