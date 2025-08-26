using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class MedicalOverviewExecutor : IToolExecutor
    {
        public string Name => "get_medical_overview";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetMedicalOverviewAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var m = t.Result;
                object res = new
                {
                    ok = true,
                    medical = new
                    {
                        summary = new
                        {
                            totalColonists = m?.Summary?.TotalColonists ?? 0,
                            patientsNeedingTend = m?.Summary?.PatientsNeedingTend ?? 0,
                            bleedingCount = m?.Summary?.BleedingCount ?? 0,
                            infectionCount = m?.Summary?.InfectionCount ?? 0,
                            operationsPending = m?.Summary?.OperationsPending ?? 0,
                            lifeThreatCount = m?.Summary?.LifeThreatCount ?? 0,
                            avgHealthPct = m?.Summary?.AvgHealthPct ?? 0f,
                            avgPainPct = m?.Summary?.AvgPainPct ?? 0f,
                            riskScore = m?.Summary?.RiskScore ?? 0
                        },
                        groups = new
                        {
                            bleeding = (m?.Groups?.Bleeding ?? new List<MedicalBleedingItem>()).Select(b => new { pawn = b.Pawn, pawnLoadId = b.PawnLoadId, bleedRate = b.BleedRate, timeToDeathHours = b.TimeToDeathHours, tended = b.Tended, inMedicalBed = b.InMedicalBed }),
                            infections = (m?.Groups?.Infections ?? new List<MedicalInfectionItem>()).Select(i => new { pawn = i.Pawn, pawnLoadId = i.PawnLoadId, disease = i.Disease, severity = i.Severity, immunity = i.Immunity, delta = i.Delta, tended = i.Tended }),
                            operations = (m?.Groups?.Operations ?? new List<MedicalOperationItem>()).Select(o => new { pawn = o.Pawn, pawnLoadId = o.PawnLoadId, billLabel = o.BillLabel, partLabel = o.PartLabel, requiresMedicine = o.RequiresMedicine, usesGlitter = o.UsesGlitter, anesthetize = o.Anesthetize, riskHint = o.RiskHint }),
                            lifeThreats = (m?.Groups?.LifeThreats ?? new List<MedicalLifeThreatItem>()).Select(l => new { pawn = l.Pawn, pawnLoadId = l.PawnLoadId, hediff = l.Hediff, severity = l.Severity, reason = l.Reason })
                        },
                        pawns = (m?.Pawns ?? new List<MedicalPawnItem>()).Select(p => new
                        {
                            pawn = p.Pawn,
                            pawnLoadId = p.PawnLoadId,
                            map = p.Map,
                            isDowned = p.IsDowned,
                            inMedicalBed = p.InMedicalBed,
                            needsTending = p.NeedsTending,
                            healthPct = p.HealthPct,
                            painPct = p.PainPct,
                            bleedRate = p.BleedRate,
                            capacities = p.Capacities == null ? null : new { consciousness = p.Capacities.Consciousness, moving = p.Capacities.Moving, manipulation = p.Capacities.Manipulation, sight = p.Capacities.Sight, hearing = p.Capacities.Hearing, talking = p.Capacities.Talking, breathing = p.Capacities.Breathing, bloodPumping = p.Capacities.BloodPumping, bloodFiltration = p.Capacities.BloodFiltration },
                            parts = (p.Parts ?? new List<MedicalPartItem>()).Select(pa => new { partLabel = pa.PartLabel, tag = pa.Tag, isMissing = pa.IsMissing, hpPct = pa.HpPct, prostheticTier = pa.ProstheticTier }),
                            hediffs = (p.Hediffs ?? new List<MedicalHediffItem>()).Select(h => new
                            {
                                defName = h.DefName,
                                label = h.Label,
                                category = h.Category,
                                stageLabel = h.StageLabel,
                                severity = h.Severity,
                                isBleeding = h.IsBleeding,
                                isTendable = h.IsTendable,
                                tended = h.Tended,
                                tendQuality = h.TendQuality,
                                tendTicksLeft = h.TendTicksLeft,
                                isInfection = h.IsInfection,
                                immunity = h.Immunity,
                                isPermanent = h.IsPermanent,
                                isAddiction = h.IsAddiction,
                                isDisease = h.IsDisease,
                                isTemperature = h.IsTemperature,
                                onPartLabel = h.OnPartLabel
                            }),
                            scheduledOps = (p.ScheduledOps ?? new List<MedicalOperationItem>()).Select(o => new { billLabel = o.BillLabel, partLabel = o.PartLabel, requiresMedicine = o.RequiresMedicine, usesGlitter = o.UsesGlitter, anesthetize = o.Anesthetize, riskHint = o.RiskHint })
                        }),
                        advisories = m?.Advisories ?? new List<string>()
                    }
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
