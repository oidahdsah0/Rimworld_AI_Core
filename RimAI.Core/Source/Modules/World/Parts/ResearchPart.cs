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
    // v1：列出当前研究、可立即开研的项目（TopN），以及关键受限项（缺前置/台/图纸）
    internal sealed class ResearchPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        private const int DefaultTopN = 12;

        public ResearchPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("ResearchPart requires ConfigurationService");
        }

        public Task<ResearchOptionsSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var maps = Find.Maps;
                if (maps == null || maps.Count == 0) throw new WorldDataException("No maps");

                // 殖民地研究能力估算：研究点/天
                float effPerDay = EstimateColonyResearchPerDay();
                var colony = new ResearchColonyInfo { Researchers = CountPotentialResearchers(), EffectiveSpeed = effPerDay };

                // 当前研究
                ResearchCurrentInfo current = null;
                var rm = Find.ResearchManager;
                var cur = rm?.GetProject();
        if (cur != null)
                {
                    float progressPct = cur.ProgressPercent;
                    float workLeft = Math.Max(0f, cur.Cost - cur.ProgressReal);
                    float etaDays = effPerDay > 0.01f ? workLeft / effPerDay : -1f;
                    current = new ResearchCurrentInfo
                    {
                        DefName = cur.defName,
                        Label = SafeLabel(cur),
                        ProgressPct = progressPct,
                        WorkLeft = workLeft,
                        EtaDays = etaDays
                    };
                }

                // 所有项目
                var all = DefDatabase<ResearchProjectDef>.AllDefsListForReading;
                var available = new List<ResearchOptionItem>();
                var lockedKey = new List<ResearchLockedItem>();

                foreach (var p in all)
                {
                    try
                    {
                        if (p == null) continue;
                        if (p.IsFinished) continue;
                        if (p.baseCost <= 0f && p.knowledgeCost <= 0f) continue; // 过滤非消耗型/隐藏项

                        bool canStart = p.CanStartNow;
                        if (canStart)
                        {
                            available.Add(ToOption(p, effPerDay));
                        }
                        else
                        {
                            var locked = AnalyzeLocked(p);
                            if (locked != null)
                            {
                                lockedKey.Add(locked);
                            }
                        }
                    }
                    catch { }
                }

                // TopN 排序：优先低成本、低 techprint、已满足前置
                available = available
                    .OrderBy(x => x.BaseCost <= 0 ? 1e9f : x.BaseCost)
                    .ThenBy(x => x.TechprintsNeeded)
                    .ThenBy(x => x.Label)
                    .Take(DefaultTopN)
                    .ToList();

                // 锁定关键项：取少量代表
                lockedKey = lockedKey
                    .OrderBy(x => (x.TechprintsMissing > 0 ? 1 : 0) + (x.BenchesMissing?.Count ?? 0) + (x.MissingPrereqs?.Count ?? 0))
                    .ThenBy(x => x.Label)
                    .Take(16)
                    .ToList();

                return new ResearchOptionsSnapshot
                {
                    Current = current,
                    AvailableNow = available,
                    LockedKey = lockedKey,
                    Colony = colony
                };
            }, name: "ResearchPart.Get", ct: cts.Token);
        }

        public Task<bool> IsFinishedAsync(string defName, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(defName)) return false;
                    var def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
                    return def != null && def.IsFinished;
                }
                catch { return false; }
            }, name: "Research.IsFinished", ct: cts.Token);
        }

        // 主线程立即查询：调用方需确保在主线程（通常用于 UI gate）
        public bool IsFinishedNow(string defName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(defName)) return false;
                var def = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
                return def != null && def.IsFinished;
            }
            catch { return false; }
        }

        private static ResearchOptionItem ToOption(ResearchProjectDef p, float effPerDay)
        {
            float cost = p.Cost;
            float left = Math.Max(0f, cost - p.ProgressReal);
            float roughDays = effPerDay > 0.01f ? left / effPerDay : -1f;
            var prereqNames = (p.prerequisites ?? new List<ResearchProjectDef>()).Select(r => r?.label ?? r?.defName).Where(s => !string.IsNullOrEmpty(s)).ToList();
            var benches = new List<string>();
            if (p.requiredResearchBuilding != null) benches.Add(p.requiredResearchBuilding.label ?? p.requiredResearchBuilding.defName);
            if (p.requiredResearchFacilities != null)
            {
                benches.AddRange(p.requiredResearchFacilities.Select(f => f?.label ?? f?.defName).Where(s => !string.IsNullOrEmpty(s)));
            }
            return new ResearchOptionItem
            {
                DefName = p.defName,
                Label = SafeLabel(p),
                Desc = SafeDesc(p),
                BaseCost = cost,
                TechLevel = p.techLevel.ToString(),
                Prereqs = prereqNames,
                Benches = benches,
                TechprintsNeeded = Math.Max(0, p.TechprintCount - p.TechprintsApplied),
                RoughTimeDays = roughDays
            };
        }

        private static ResearchLockedItem AnalyzeLocked(ResearchProjectDef p)
        {
            var missingPrereqs = new List<string>();
            if (p.prerequisites != null)
            {
                foreach (var r in p.prerequisites)
                {
                    if (r != null && !r.IsFinished) missingPrereqs.Add(SafeLabel(r) ?? r.defName);
                }
            }
            if (p.hiddenPrerequisites != null)
            {
                foreach (var r in p.hiddenPrerequisites)
                {
                    if (r != null && !r.IsFinished) missingPrereqs.Add(SafeLabel(r) ?? r.defName);
                }
            }

            var benchesMissing = new List<string>();
            // 若指定了需要的研究台/设施但未满足 PlayerHasAnyAppropriateResearchBench，则认为缺失
            bool hasBench = p.PlayerHasAnyAppropriateResearchBench;
            if (!hasBench)
            {
                if (p.requiredResearchBuilding != null) benchesMissing.Add(p.requiredResearchBuilding.label ?? p.requiredResearchBuilding.defName);
                if (p.requiredResearchFacilities != null)
                {
                    benchesMissing.AddRange(p.requiredResearchFacilities.Select(f => f?.label ?? f?.defName).Where(s => !string.IsNullOrEmpty(s)));
                }
            }

            int techprintsMissing = 0;
            if (p.TechprintCount > 0)
            {
                techprintsMissing = Math.Max(0, p.TechprintCount - p.TechprintsApplied);
            }

            // 如果所有条件都满足但仍不可研究，多半是特殊检查（如重力引擎检查/隐藏/分析等）
            string note = null;
            if ((missingPrereqs.Count == 0) && (benchesMissing.Count == 0) && (techprintsMissing == 0))
            {
                if (!p.InspectionRequirementsMet) note = "inspection_required";
                else if (!p.AnalyzedThingsRequirementsMet) note = "analysis_required";
                else if (p.IsHidden) note = "hidden";
                else if (p.requiresMechanitor && !p.PlayerMechanitorRequirementMet) note = "mechanitor_required";
                else note = "unknown_blocker";
            }

            // 若没有缺失任何关键条件，返回 null 以减少噪音
            if (missingPrereqs.Count == 0 && benchesMissing.Count == 0 && techprintsMissing == 0 && note == null) return null;

            return new ResearchLockedItem
            {
                DefName = p.defName,
                Label = SafeLabel(p),
                MissingPrereqs = missingPrereqs,
                BenchesMissing = benchesMissing,
                TechprintsMissing = techprintsMissing,
                Note = note
            };
        }

        private static string SafeDesc(ResearchProjectDef p)
        {
            try { return p.Description; } catch { return string.Empty; }
        }

        private static string SafeLabel(ResearchProjectDef p)
        {
            try
            {
                var s = p.LabelCap.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            catch { }
            try { if (!string.IsNullOrWhiteSpace(p.label)) return p.label; } catch { }
            try { if (!string.IsNullOrWhiteSpace(p.defName)) return p.defName; } catch { }
            return string.Empty;
        }

        private static int CountPotentialResearchers()
        {
            int n = 0;
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
            foreach (var pawn in pawns)
            {
                try
                {
                    if (pawn.Dead || pawn.Downed) continue;
                    if (pawn.workSettings == null || !pawn.workSettings.EverWork) continue;
                    if (pawn.WorkTagIsDisabled(WorkTags.Intellectual)) continue;
                    // 是否分配研究工作（不强制，但用于估算研究者数量）
                    var def = WorkTypeDefOf.Research;
                    if (def != null && pawn.WorkTypeIsDisabled(def)) continue;
                    if (pawn.skills?.GetSkill(SkillDefOf.Intellectual)?.TotallyDisabled == true) continue;
                    n++;
                }
                catch { }
            }
            return n;
        }

        private static float EstimateColonyResearchPerDay()
        {
            // 近似：对所有可研究的殖民者，叠加 ResearchSpeed × 现实作业系数 × 基准换算
            // 研究点的核心换算在 ResearchManager.ResearchPerformed：amount(Work) * 0.00825 * difficulty * techFactor
            // 这里的 effPerDay 使用一个保守假设：每日有效研究工时 ~ 6 小时；工作台因子 ~ 1.0
            // 因此：每位研究者的每日研究点 ~= ResearchSpeed * 6h * 2500tick/h * 0.00825 / 60k tick/day
            const float ticksPerHour = 2500f;
            const float ticksPerDay = 60000f;
            const float baseFactor = 0.00825f;
            const float activeHours = 6f;
            float difficulty = 1f;
            try { difficulty = Find.Storyteller?.difficulty?.researchSpeedFactor ?? 1f; } catch { }

            float sum = 0f;
            var pawns = PawnsFinder.AllMaps_FreeColonistsSpawned;
            foreach (var pawn in pawns)
            {
                try
                {
                    if (pawn.Dead || pawn.Downed) continue;
                    if (pawn.WorkTagIsDisabled(WorkTags.Intellectual)) continue;
                    float rs = pawn.GetStatValue(StatDefOf.ResearchSpeed);
                    // bench factor 由具体工作时计算；此处取 1.0 近似
                    float perDay = rs * activeHours * ticksPerHour * baseFactor * difficulty / ticksPerDay;
                    sum += perDay;
                }
                catch { }
            }
            // 科技等级差异会在实际研究中按项目 CostFactor 生效；这里作为粗略速度不再二次扣减
            return sum;
        }
    }
}
