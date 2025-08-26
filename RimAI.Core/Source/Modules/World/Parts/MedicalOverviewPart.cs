using System;
using System.Collections.Generic;
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
    // v1：健康检查（汇总优先；分组与明细可裁剪）
    internal sealed class MedicalOverviewPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public MedicalOverviewPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("MedicalOverviewPart requires ConfigurationService");
        }

        public Task<MedicalOverviewSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");

                var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
                int total = pawns.Count;
                int needTend = 0, bleeding = 0, infected = 0, ops = 0, lifeThreat = 0;
                float sumHealth = 0f, sumPain = 0f;

                var groups = new MedicalGroups
                {
                    Bleeding = new List<MedicalBleedingItem>(),
                    Infections = new List<MedicalInfectionItem>(),
                    Operations = new List<MedicalOperationItem>(),
                    LifeThreats = new List<MedicalLifeThreatItem>()
                };
                var pawnItems = new List<MedicalPawnItem>(Math.Max(8, total));
                var advisories = new List<string>();

                foreach (var p in pawns)
                {
                    try
                    {
                        var hs = p.health?.hediffSet;
                        var summaryHealth = p.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
                        var pain = hs?.PainTotal ?? 0f;
                        sumHealth += summaryHealth;
                        sumPain += pain;

                        bool needsTending = hs?.HasTendableHediff() ?? false;
                        if (needsTending) needTend++;

                        float bleedRate = hs?.BleedRateTotal ?? 0f;
                        bool isBleeding = bleedRate > 0.0001f;
                        if (isBleeding)
                        {
                            bleeding++;
                            int ticks = 0;
                            try { ticks = HealthUtility.TicksUntilDeathDueToBloodLoss(p); } catch { ticks = -1; }
                            var medBed = (p.CurrentBed() != null) && p.CurrentBed().def.IsBed && p.CurrentBed().Medical; // 近似
                            bool tendedAny = false;
                            try
                            {
                                foreach (var h in hs.hediffs)
                                {
                                    if (h is Hediff_Injury || h is Hediff_MissingPart) continue;
                                    if (h.TryGetComp<HediffComp_TendDuration>() is HediffComp_TendDuration td && td.IsTended) { tendedAny = true; break; }
                                }
                            }
                            catch { }
                            (groups.Bleeding as List<MedicalBleedingItem>).Add(new MedicalBleedingItem
                            {
                                Pawn = p.LabelShortCap.ToString(),
                                PawnLoadId = p.thingIDNumber,
                                BleedRate = bleedRate,
                                TimeToDeathHours = ticks > 0 ? ticks / 2500f : -1f,
                                Tended = tendedAny,
                                InMedicalBed = medBed
                            });
                        }

                        try
                        {
                            Hediff threat; BodyPartRecord part;
                            if (HealthUtility.TryGetWorstHealthCondition(p, out threat, out part) && threat != null && threat.IsCurrentlyLifeThreatening && !threat.FullyImmune())
                            {
                                lifeThreat++;
                                (groups.LifeThreats as List<MedicalLifeThreatItem>).Add(new MedicalLifeThreatItem
                                {
                                    Pawn = p.LabelShortCap.ToString(),
                                    PawnLoadId = p.thingIDNumber,
                                    Hediff = threat.def?.label ?? threat.def?.defName,
                                    Severity = SafeSeverity(threat),
                                    Reason = threat.CurStage?.label ?? "life_threatening"
                                });
                            }
                        }
                        catch { }

                        // 感染（免疫性）
                        try
                        {
                            foreach (var h in hs?.hediffs ?? Enumerable.Empty<Hediff>())
                            {
                                var comp = h.TryGetComp<HediffComp_Immunizable>();
                                if (comp == null) continue;
                                float sev = SafeSeverity(h);
                                float imm = comp.Immunity;
                                (groups.Infections as List<MedicalInfectionItem>).Add(new MedicalInfectionItem
                                {
                                    Pawn = p.LabelShortCap.ToString(),
                                    PawnLoadId = p.thingIDNumber,
                                    Disease = h.def?.label ?? h.def?.defName,
                                    Severity = sev,
                                    Immunity = imm,
                                    Delta = imm - sev,
                                    Tended = h.IsTended()
                                });
                                infected++;
                            }
                        }
                        catch { }

                        // 手术计划
                        try
                        {
                            var bills = p.BillStack?.Bills;
                            if (bills != null)
                            {
                                foreach (var b in bills)
                                {
                                    if (b is Bill_Medical bm)
                                    {
                                        ops++;
                                        (groups.Operations as List<MedicalOperationItem>).Add(new MedicalOperationItem
                                        {
                                            Pawn = p.LabelShortCap.ToString(),
                                            PawnLoadId = p.thingIDNumber,
                                            BillLabel = bm.LabelCap.ToString(),
                                            PartLabel = bm.Part?.LabelCap ?? string.Empty,
                                            RequiresMedicine = bm.recipe != null, // v1：保守认为医疗手术可能需要用药
                                            UsesGlitter = false, // 运行时推断较难，先占位
                                            Anesthetize = bm.recipe?.anesthetize ?? false,
                                            RiskHint = null
                                        });
                                    }
                                }
                            }
                        }
                        catch { }

                        // 逐人明细（v1 先含 overall + hediffs）
                        var heItems = new List<MedicalHediffItem>();
                        try
                        {
                            foreach (var h in hs?.hediffs ?? new List<Hediff>())
                            {
                                if (h == null) continue;
                                heItems.Add(ToHediffItem(h));
                            }
                        }
                        catch { }

                        // 能力
                        MedicalCapacities caps = null;
                        try
                        {
                            float Lv(PawnCapacityDef def) { try { return Mathf.Clamp01(p.health.capacities.GetLevel(def)); } catch { return 0f; } }
                            caps = new MedicalCapacities
                            {
                                Consciousness = Lv(PawnCapacityDefOf.Consciousness),
                                Moving = Lv(PawnCapacityDefOf.Moving),
                                Manipulation = Lv(PawnCapacityDefOf.Manipulation),
                                Sight = Lv(PawnCapacityDefOf.Sight),
                                Hearing = Lv(PawnCapacityDefOf.Hearing),
                                Talking = Lv(PawnCapacityDefOf.Talking),
                                Breathing = Lv(PawnCapacityDefOf.Breathing),
                                BloodPumping = Lv(PawnCapacityDefOf.BloodPumping),
                                BloodFiltration = Lv(PawnCapacityDefOf.BloodFiltration)
                            };
                        }
                        catch { }

                        // 部位 hpPct（TopN 可在未来裁剪；v1 全量输出主要部位）
                        List<MedicalPartItem> parts = null;
                        try
                        {
                            parts = new List<MedicalPartItem>();
                            var body = p.RaceProps?.body;
                            if (body != null && hs != null)
                            {
                                foreach (var part in hs.GetNotMissingParts())
                                {
                                    float cur = 0f, max = 1f;
                                    try { cur = hs.GetPartHealth(part); max = part.def.GetMaxHealth(p); } catch { }
                                    float hpPct = (max > 0.0001f) ? Mathf.Clamp01(cur / max) : 1f;
                                    string tier = "none";
                                    try
                                    {
                                        // 简单判断：如果该部位或子部位存在 Hediff_AddedPart，则视为义肢，按 label 推断档位
                                        bool hasAdded = hs.hediffs.Any(h => h is Hediff_AddedPart && h.Part == part);
                                        if (hasAdded)
                                        {
                                            var lab = hs.hediffs.First(h => h is Hediff_AddedPart && h.Part == part).LabelCap.ToString().ToLowerInvariant();
                                            if (lab.Contains("archo") || lab.Contains("超科技")) tier = "archotech";
                                            else if (lab.Contains("bionic") || lab.Contains("仿生")) tier = "bionic";
                                            else tier = "prosthetic";
                                        }
                                    }
                                    catch { }
                                    parts.Add(new MedicalPartItem
                                    {
                                        PartLabel = part.LabelCap,
                                        Tag = part.def.tags != null && part.def.tags.Count > 0 ? part.def.tags[0]?.defName : string.Empty,
                                        IsMissing = false,
                                        HpPct = hpPct,
                                        ProstheticTier = tier
                                    });
                                }
                                // 缺失部位
                                try
                                {
                                    var missing = hs.GetMissingPartsCommonAncestors();
                                    if (missing != null)
                                    {
                                        foreach (var mp in missing)
                                        {
                                            parts.Add(new MedicalPartItem
                                            {
                                                PartLabel = mp.Part?.LabelCap ?? "(missing)",
                                                Tag = mp.Part?.def?.defName,
                                                IsMissing = true,
                                                HpPct = 0f,
                                                ProstheticTier = "none"
                                            });
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        pawnItems.Add(new MedicalPawnItem
                        {
                            Pawn = p.LabelShortCap.ToString(),
                            PawnLoadId = p.thingIDNumber,
                            Map = p.Map?.info?.parent?.LabelCap ?? p.Map?.ToString() ?? string.Empty,
                            IsDowned = p.Downed,
                            InMedicalBed = (p.CurrentBed() != null) && p.CurrentBed().def.IsBed && p.CurrentBed().Medical,
                            NeedsTending = needsTending,
                            HealthPct = summaryHealth,
                            PainPct = pain,
                            BleedRate = bleedRate,
                            Capacities = caps,
                            Parts = parts ?? new List<MedicalPartItem>(),
                            Hediffs = heItems,
                            ScheduledOps = (groups.Operations as List<MedicalOperationItem>).Where(x => x.PawnLoadId == p.thingIDNumber).ToList()
                        });
                    }
                    catch { }
                }

                int risk = 0;
                // 轻量评分：生命危险/出血即将死亡/感染落后
                try
                {
                    risk += lifeThreat * 40;
                    foreach (var b in groups.Bleeding)
                    {
                        if (b.TimeToDeathHours > 0 && b.TimeToDeathHours < 5f) risk += 25;
                        else if (b.TimeToDeathHours > 0 && b.TimeToDeathHours < 12f) risk += 15;
                    }
                    foreach (var inf in groups.Infections)
                    {
                        if (inf.Delta < 0f) risk += 15;
                        if (inf.Delta < -0.1f) risk += 10;
                    }
                    risk = Math.Max(0, Math.Min(100, risk));
                }
                catch { }

                // 提示
                if (bleeding > 0)
                {
                    var urgent = groups.Bleeding.Where(x => x.TimeToDeathHours > 0 && x.TimeToDeathHours < 5f).Select(x => x.Pawn).ToList();
                    if (urgent.Count > 0)
                    {
                        advisories.Add($"{urgent.Count}人出血将于<5h死亡：{string.Join(",", urgent)}");
                    }
                }
                if (infected > 0 && groups.Infections.Any(x => x.Delta < 0f))
                {
                    var names = groups.Infections.Where(x => x.Delta < 0f).Select(x => x.Pawn).Distinct().ToList();
                    advisories.Add($"{names.Count}人感染免疫落后：{string.Join(",", names)}");
                }
                if (ops > 0) advisories.Add($"有 {ops} 项手术计划待执行");

                var summary = new MedicalSummary
                {
                    TotalColonists = total,
                    PatientsNeedingTend = needTend,
                    BleedingCount = bleeding,
                    InfectionCount = infected,
                    OperationsPending = ops,
                    LifeThreatCount = lifeThreat,
                    AvgHealthPct = total > 0 ? (sumHealth / total) : 0f,
                    AvgPainPct = total > 0 ? (sumPain / total) : 0f,
                    RiskScore = risk
                };

                return new MedicalOverviewSnapshot
                {
                    Summary = summary,
                    Groups = groups,
                    Pawns = pawnItems,
                    Advisories = advisories
                };
            }, name: "MedicalOverviewPart.Get", ct: cts.Token);
        }

        private static float SafeSeverity(Hediff h)
        {
            try { return h?.Severity ?? 0f; } catch { return 0f; }
        }

        private static MedicalHediffItem ToHediffItem(Hediff h)
        {
            var item = new MedicalHediffItem();
            try
            {
                item.DefName = h.def?.defName;
                item.Label = h.LabelBaseCap ?? h.LabelCap ?? h.def?.label ?? h.def?.defName;
                item.StageLabel = h.CurStage?.label;
                item.Severity = SafeSeverity(h);
                item.OnPartLabel = h.Part?.LabelCap ?? string.Empty;
                item.IsPermanent = h.IsPermanent();
                item.IsTendable = h.TendableNow();
                item.Tended = h.IsTended();
                var td = h.TryGetComp<HediffComp_TendDuration>();
                item.TendQuality = td?.tendQuality ?? 0f;
                item.TendTicksLeft = td?.tendTicksLeft ?? 0;
                var imm = h.TryGetComp<HediffComp_Immunizable>();
                item.IsInfection = imm != null;
                item.Immunity = imm?.Immunity ?? 0f;
                item.IsAddiction = h is Hediff_Addiction;
                item.IsDisease = h.def?.isBad == true && !(h is Hediff_Injury) && !(h is Hediff_MissingPart);
                item.IsTemperature = (h.def == HediffDefOf.Hypothermia || h.def == HediffDefOf.Heatstroke);
                try { item.IsBleeding = h.Bleeding; } catch { item.IsBleeding = false; }
                // 分类
                string cat = "other";
                if (h is Hediff_MissingPart) cat = "missing";
                else if (h is Hediff_AddedPart) cat = "implant";
                else if (h.def?.injuryProps != null) cat = "injury";
                else if (item.IsAddiction) cat = "addiction";
                else if (item.IsTemperature) cat = "temperature";
                else if (item.IsDisease) cat = "disease";
                item.Category = cat;
            }
            catch { }
            return item;
        }
    }
}
