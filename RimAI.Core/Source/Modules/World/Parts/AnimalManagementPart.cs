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
    internal sealed class AnimalManagementPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public AnimalManagementPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<AnimalManagementSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => BuildSnapshotSafe(), name: "GetAnimalManagement", ct: cts.Token);
        }

        private AnimalManagementSnapshot BuildSnapshotSafe()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return new AnimalManagementSnapshot
                {
                    Counts = new AnimalCounts { Total = 0, Species = Array.Empty<SpeciesCountItem>() },
                    Training = new AnimalTrainingSummary
                    {
                        Obedience = new TrainableStat(), Release = new TrainableStat(), Rescue = new TrainableStat(), Haul = new TrainableStat()
                    },
                    Food = new AnimalFoodStatus { TotalNutrition = 0, DailyNeed = 0, Days = 0, Sources = Array.Empty<FoodSourceItem>() }
                };

                // 1) 收集殖民地动物
                var animals = map.mapPawns.ColonyAnimals?.Where(p => p != null && p.Spawned && p.RaceProps?.Animal == true).ToList() ?? new List<Pawn>();

                // 2) 物种计数
                var species = new List<SpeciesCountItem>();
                foreach (var g in animals.GroupBy(a => a.def))
                {
                    var def = g.Key;
                    if (def == null) continue;
                    species.Add(new SpeciesCountItem
                    {
                        DefName = def.defName,
                        Label = def.label ?? def.defName,
                        Count = g.Count()
                    });
                }
                species = species.OrderByDescending(s => s.Count).ToList();

                // 3) 训练统计（Obedience/Release/Rescue/Haul）
                var obedience = new TrainableStat();
                var release = new TrainableStat();
                var rescue = new TrainableStat();
                var haul = new TrainableStat();
                // 可选训练定义：Rescue/Haul 在当前版本可能不存在，使用名称查找以兼容不同版本
                TrainableDef defRescue = null;
                TrainableDef defHaul = null;
                try { defRescue = DefDatabase<TrainableDef>.GetNamedSilentFail("Rescue"); } catch { }
                try { defHaul = DefDatabase<TrainableDef>.GetNamedSilentFail("Haul"); } catch { }
                foreach (var a in animals)
                {
                    try
                    {
                        var tr = a.training;
                        if (tr != null)
                        {
                            // 可训练（Eligible）：CanAssignToTrain.Accepted
                            // 已掌握（Learned）：HasLearned
                            if (tr.CanAssignToTrain(TrainableDefOf.Obedience).Accepted) obedience.Eligible++;
                            if (tr.HasLearned(TrainableDefOf.Obedience)) obedience.Learned++;

                            if (tr.CanAssignToTrain(TrainableDefOf.Release).Accepted) release.Eligible++;
                            if (tr.HasLearned(TrainableDefOf.Release)) release.Learned++;

                            if (defRescue != null)
                            {
                                if (tr.CanAssignToTrain(defRescue).Accepted) rescue.Eligible++;
                                if (tr.HasLearned(defRescue)) rescue.Learned++;
                            }

                            if (defHaul != null)
                            {
                                if (tr.CanAssignToTrain(defHaul).Accepted) haul.Eligible++;
                                if (tr.HasLearned(defHaul)) haul.Learned++;
                            }
                        }
                    }
                    catch { }
                }

                // 4) 饲料来源统计（TotalNutrition）与动物日需求估算（DailyNeed）
                var foodSources = new List<FoodSourceItem>();
                float totalNutrition = 0f;
                try { map.resourceCounter?.UpdateResourceCounts(); } catch { }
                var all = map.resourceCounter?.AllCountedAmounts ?? new Dictionary<ThingDef, int>();
                foreach (var kv in all)
                {
                    var def = kv.Key; var count = kv.Value;
                    if (def == null || count <= 0) continue;
                    // 动物可食：包含动物可食（人类不可食也可，例如干草/ kibble）
                    if (!def.IsNutritionGivingIngestible) continue;
                    // v1：排除只给人类食用的
                    bool humanOnly = false; try { humanOnly = def.ingestible?.HumanEdible == true && def.ingestible?.preferability == FoodPreferability.MealLavish; } catch { humanOnly = false; }
                    // 但动物也能吃生肉/蔬菜等，人类可食不必排除。我们只排除明确的奢华饭等
                    if (humanOnly) continue;
                    float nutPer = 0f; try { nutPer = def.GetStatValueAbstract(StatDefOf.Nutrition); } catch { }
                    if (nutPer <= 0f) continue;
                    float sum = nutPer * count;
                    totalNutrition += sum;
                    foodSources.Add(new FoodSourceItem { DefName = def.defName, Label = def.label ?? def.defName, Count = count, NutritionPer = nutPer, TotalNutrition = sum });
                }
                foodSources = foodSources.OrderByDescending(f => f.TotalNutrition).ToList();

                // 计算动物总日需求：基于饥饿速率 HungerRate（或食物消耗率）近似
                float dailyNeed = 0f;
                foreach (var a in animals)
                {
                    try
                    {
                        // 饥饿速率：pawn.needs.food.FoodFallPerTickAssumingCategory(???)+食物营养转化复杂；v1 采用近似：体型 × 常数
                        // 参考：标准成年殖民者基础日营养 ~ 2；动物按体型比例放大。
                        float bodySize = 1f; try { bodySize = a.RaceProps?.baseBodySize ?? 1f; } catch { bodySize = 1f; }
                        // 系数：每体型单位每日约 1.6 营养（经验近似）。
                        dailyNeed += 1.6f * Math.Max(0.2f, bodySize);
                    }
                    catch { }
                }

                float days = (dailyNeed > 0f) ? (totalNutrition / dailyNeed) : 0f;

                return new AnimalManagementSnapshot
                {
                    Counts = new AnimalCounts { Total = animals.Count, Species = species },
                    Training = new AnimalTrainingSummary { Obedience = obedience, Release = release, Rescue = rescue, Haul = haul },
                    Food = new AnimalFoodStatus { TotalNutrition = totalNutrition, DailyNeed = dailyNeed, Days = days, Sources = foodSources }
                };
            }
            catch (Exception ex)
            {
                try { Verse.Log.Warning($"[RimAI.Core] Animal management failed: {ex.Message}"); } catch { }
                return new AnimalManagementSnapshot
                {
                    Counts = new AnimalCounts { Total = 0, Species = Array.Empty<SpeciesCountItem>() },
                    Training = new AnimalTrainingSummary { Obedience = new TrainableStat(), Release = new TrainableStat(), Rescue = new TrainableStat(), Haul = new TrainableStat() },
                    Food = new AnimalFoodStatus { TotalNutrition = 0, DailyNeed = 0, Days = 0, Sources = Array.Empty<FoodSourceItem>() }
                };
            }
        }
    }
}
