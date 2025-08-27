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
    internal sealed class SubspaceActionPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public SubspaceActionPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<SubspaceInvocationOutcome> TryInvokeAsync(int llmScore, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(Mathf.Max(timeoutMs, 3000));
            llmScore = Mathf.Clamp(llmScore, 0, 100);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    if (Current.Game == null) return (SubspaceInvocationOutcome)null;
                    var map = Find.CurrentMap ?? Find.Maps?.FirstOrDefault();
                    if (map == null) return (SubspaceInvocationOutcome)null;

                    // Tier mapping by score
                    string tier = llmScore >= 85 ? "apex" : llmScore >= 65 ? "high" : llmScore >= 35 ? "mid" : "low";

                    // DLC/defs detection
                    bool hasRevenant = DefDatabase<PawnKindDef>.GetNamedSilentFail("Revenant") != null;
                    bool hasShambler = DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSoldier") != null || DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSwarmer") != null;

                    // Choose composition: prefer mixed insects+revenant when revenant exists; else insects+shamblers if shambler exists; else insects only
                    string composition = hasRevenant ? "mixed" : hasShambler ? "shamblers" : "insects";

                    // Build spawn plan
                    var pawns = new List<Pawn>();
                    int targetCount = tier switch { "apex" => 8, "high" => 6, "mid" => 4, _ => 2 };
                    int spawned = 0;
                    IntVec3 dropCenter = DropCellFinder.TradeDropSpot(map);
                    Faction hostileFaction = Find.FactionManager?.FirstFactionOfDef(FactionDefOf.Insect) ?? Faction.OfInsects;

                    // Try incidents first when DLC present (simple attempts, silent failures)
                    bool incidentTriggered = false;
                    try
                    {
                        if (hasRevenant)
                        {
                            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall, map);
                            parms.points = Mathf.Max(120f, parms.points * (tier == "apex" ? 2.5f : tier == "high" ? 1.8f : tier == "mid" ? 1.2f : 0.8f));
                            var inc = DefDatabase<IncidentDef>.GetNamedSilentFail("Revenant");
                            if (inc != null && inc.Worker?.CanFireNow(parms) == true)
                            {
                                incidentTriggered = inc.Worker.TryExecute(parms);
                            }
                        }
                    }
                    catch { incidentTriggered = false; }

                    if (!incidentTriggered)
                    {
                        // Fallback: direct spawn pawns
                        IEnumerable<PawnKindDef> kinds = Enumerable.Empty<PawnKindDef>();
                        try
                        {
                            var list = new List<PawnKindDef>();
                            if (hasRevenant)
                            {
                                var rev = DefDatabase<PawnKindDef>.GetNamedSilentFail("Revenant");
                                if (rev != null) list.Add(rev);
                            }
                            if (hasShambler)
                            {
                                var sh1 = DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSoldier");
                                var sh2 = DefDatabase<PawnKindDef>.GetNamedSilentFail("ShamblerSwarmer");
                                if (sh1 != null) list.Add(sh1);
                                if (sh2 != null) list.Add(sh2);
                            }
                            // Always include insects as fallback
                            var insectKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                                .Where(k => k?.race?.race?.FleshType == FleshTypeDefOf.Insectoid)
                                .ToList();
                            if (insectKinds != null && insectKinds.Count > 0)
                            {
                                // pick some representative insects
                                foreach (var k in insectKinds.Take(4)) list.Add(k);
                            }
                            kinds = list.Where(k => k != null).ToList();
                        }
                        catch { kinds = Enumerable.Empty<PawnKindDef>(); }

                        var rand = new System.Random(unchecked(Environment.TickCount ^ llmScore));
                        for (int i = 0; i < targetCount; i++)
                        {
                            try
                            {
                                var kind = kinds.OrderBy(_ => rand.Next()).FirstOrDefault();
                                if (kind == null) break;
                                if (!CellFinder.TryFindRandomCellNear(dropCenter, map, 12, c => c.Standable(map) && !c.Fogged(map), out var cell)) cell = dropCenter;
                                var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, hostileFaction, PawnGenerationContext.NonPlayer, map.Tile, forceGenerateNewPawn: true));
                                if (GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish) != null)
                                {
                                    pawns.Add(pawn);
                                    spawned++;
                                }
                            }
                            catch { }
                        }
                    }

                    // Notify user
                    try
                    {
                        var msg = "RimAI.Core.World.Subspace.Invoke.Start".Translate(tier, composition);
                        Messages.Message(msg, MessageTypeDefOf.ThreatSmall);
                    }
                    catch { }

                    return new SubspaceInvocationOutcome { Tier = tier, Composition = composition, Count = Math.Max(spawned, 0) };
                }
                catch { return null; }
            }, name: "World.TryInvokeSubspace", ct: cts.Token);
        }
    }
}
