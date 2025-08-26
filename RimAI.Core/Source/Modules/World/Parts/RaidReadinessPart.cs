using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class RaidReadinessPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public RaidReadinessPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<RaidReadinessSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(BuildSafe, name: "GetRaidReadiness", ct: cts.Token);
        }

        private RaidReadinessSnapshot BuildSafe()
        {
            try
            {
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null)
                {
                    return new RaidReadinessSnapshot
                    {
                        Wealth = new WealthInfo(),
                        Colony = new ColonyCombatInfo(),
                        Animals = new AnimalCombatInfo(),
                        Mechs = new MechCombatInfo(),
                        Points = new ThreatPointBreakdown { RandomFactorMin = 1f, RandomFactorMax = 1f }
                    };
                }

                var map = Find.CurrentMap;

                // Wealth
                float items = 0, buildings = 0, pawnsWealth = 0, total = 0;
                try
                {
                    items = map.wealthWatcher?.WealthItems ?? 0f;
                    buildings = map.wealthWatcher?.WealthBuildings ?? 0f;
                    pawnsWealth = map.wealthWatcher?.WealthPawns ?? 0f;
                    total = map.wealthWatcher?.WealthTotal ?? (items + buildings + pawnsWealth);
                }
                catch { }

                float storytellerWealth = 0f;
                try { storytellerWealth = map.PlayerWealthForStoryteller; } catch { }

                var wealth = new WealthInfo
                {
                    Total = total,
                    Items = items,
                    Buildings = buildings,
                    Pawns = pawnsWealth,
                    PlayerWealthForStoryteller = storytellerWealth
                };

                // Colony combat readiness (humans)
                int human = 0, armed = 0;
                float healthSum = 0f; int healthCount = 0;
                foreach (var p in map.mapPawns?.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>())
                {
                    human++;
                    try
                    {
                        if (p.equipment?.Primary != null) armed++;
                        healthSum += p.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                        healthCount++;
                    }
                    catch { }
                }
                var colony = new ColonyCombatInfo
                {
                    HumanCount = human,
                    ArmedCount = armed,
                    AvgHealthPct = (healthCount > 0 ? Mathf.Clamp01(healthSum / healthCount) : 1f)
                };

                // Animal combat readiness (Release-trainable, not downed)
                int battleAnimals = 0; float animalPoints = 0f;
                foreach (var a in map.mapPawns?.PawnsInFaction(Faction.OfPlayer).Where(x => x?.RaceProps?.Animal ?? false) ?? Enumerable.Empty<Pawn>())
                {
                    try
                    {
                        if (!a.Downed && a.training.CanAssignToTrain(TrainableDefOf.Release).Accepted)
                        {
                            battleAnimals++;
                            animalPoints += 0.08f * (a.kindDef?.combatPower ?? 0f);
                        }
                    }
                    catch { }
                }
                var animals = new AnimalCombatInfo { BattleReadyCount = battleAnimals, PointsContribution = animalPoints };

                // Mechs
                int mechCount = 0; float mechPower = 0f;
                foreach (var m in map.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
                {
                    try
                    {
                        if (m.IsColonyMech && !m.Downed)
                        {
                            mechCount++;
                            mechPower += m.kindDef?.combatPower ?? 0f;
                        }
                    }
                    catch { }
                }
                var mechs = new MechCombatInfo { Count = mechCount, CombatPowerSum = mechPower };

                // Threat points and factors
                float basePoints = 0f;
                try { basePoints = StorytellerUtility.DefaultThreatPointsNow(map); } catch { }

                float diffScale = 1f;
                float adapt = 1f;
                float timeFactor = 1f;
                int days = 0;
                try
                {
                    diffScale = Find.Storyteller?.difficulty?.threatScale ?? 1f;
                    var adaptation = Find.StoryWatcher?.watcherAdaptation?.TotalThreatPointsFactor ?? 1f;
                    var adaptFactor = Find.Storyteller?.difficulty?.adaptationEffectFactor ?? 1f;
                    adapt = Mathf.Lerp(1f, adaptation, adaptFactor);
                    timeFactor = Find.Storyteller?.def?.pointsFactorFromDaysPassed?.Evaluate(GenDate.DaysPassedSinceSettle) ?? 1f;
                    days = GenDate.DaysPassedSinceSettle;
                }
                catch { }

                FloatRange randRange = FloatRange.One;
                try { randRange = map.IncidentPointsRandomFactorRange; } catch { }

                var points = new ThreatPointBreakdown
                {
                    FinalPoints = basePoints,
                    DifficultyScale = diffScale,
                    AdaptationApplied = adapt,
                    TimeFactor = timeFactor,
                    RandomFactorMin = randRange.min,
                    RandomFactorMax = randRange.max,
                    DaysSinceSettle = days
                };

                // Risk band heuristics by final points
                string band = "low";
                if (basePoints >= 2000f) band = "extreme";
                else if (basePoints >= 1200f) band = "high";
                else if (basePoints >= 500f) band = "medium";

                // Size estimates: rough ranges for humanoid raids. Assume avg combatPower ~ 120 for raiders
                int HumanoidCount(float pts) => Mathf.Clamp(Mathf.RoundToInt(pts / 120f), 1, 200);
                var estimates = new List<RaidSizeEstimate>
                {
                    new RaidSizeEstimate { Archetype = "humanoids", Min = HumanoidCount(basePoints*0.8f), Max = HumanoidCount(basePoints*1.2f) },
                    new RaidSizeEstimate { Archetype = "mixed", Min = HumanoidCount(basePoints*0.6f), Max = HumanoidCount(basePoints*1.1f) }
                }; 

                return new RaidReadinessSnapshot
                {
                    Wealth = wealth,
                    Colony = colony,
                    Animals = animals,
                    Mechs = mechs,
                    Points = points,
                    RiskBand = band,
                    SizeEstimates = estimates
                };
            }
            catch (Exception ex)
            {
                try { Log.Warning($"[RimAI.Core] RaidReadiness failed: {ex.Message}"); } catch { }
                return new RaidReadinessSnapshot
                {
                    Wealth = new WealthInfo(), Colony = new ColonyCombatInfo(), Animals = new AnimalCombatInfo(), Mechs = new MechCombatInfo(),
                    Points = new ThreatPointBreakdown { RandomFactorMin = 1f, RandomFactorMax = 1f }
                };
            }
        }
    }
}
