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

                    // Choose composition label（实际直投仅使用昆虫；人形单位优先用 Incident 触发，避免背包/背景故事报错）
                    string composition = hasRevenant ? "mixed" : hasShambler ? "shamblers" : "insects";

                    // Build spawn plan
                    var pawns = new List<Pawn>();
                    // Adjust count range to 10–50 per tier
                    int targetCount = tier switch { "apex" => 50, "high" => 35, "mid" => 20, _ => 10 };
                    int spawned = 0;

                    // Preferred spawn center: hostile cluster center -> colony center -> map center
                    IntVec3 center = map.Center;
                    try
                    {
                        var hostiles = map.mapPawns?.AllPawnsSpawned?.Where(p => p != null && p.Spawned && p.HostileTo(Faction.OfPlayer))?.ToList();
                        if (hostiles != null && hostiles.Count > 0)
                        {
                            // 简化：随机选取一个敌人的当前位置作为中心
                            var idx = Verse.Rand.Range(0, hostiles.Count);
                            center = hostiles[idx].Position;
                        }
                        else
                        {
                            // 回退：随机选一个殖民者位置；再不行用地图中心
                            var colonists = map.mapPawns?.FreeColonistsSpawned?.ToList();
                            if (colonists != null && colonists.Count > 0)
                            {
                                var idx2 = Verse.Rand.Range(0, colonists.Count);
                                center = colonists[idx2].Position;
                            }
                        }
                    }
                    catch { center = map.Center; }

                    IntVec3 dropCenter;
                    if (!CellFinder.TryFindRandomCellNear(center, map, 12, c => c.Standable(map) && !c.Fogged(map), out dropCenter))
                        dropCenter = DropCellFinder.TradeDropSpot(map);

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
                        // Fallback: direct spawn pawns（仅昆虫类且非人形，避免背景故事相关报错）
                        IEnumerable<PawnKindDef> kinds = Enumerable.Empty<PawnKindDef>();
                        try
                        {
                            var list = new List<PawnKindDef>();
                            // Direct-spawn only insects (non-humanlike) and hard-exclude shambler/revenant families by name/label to avoid humanlike backstories
                            var insectKinds = DefDatabase<PawnKindDef>.AllDefsListForReading
                                .Where(k => k != null && k.race != null && k.race.race != null && !k.race.race.Humanlike && k.race.race.FleshType == FleshTypeDefOf.Insectoid)
                                .Where(k =>
                                {
                                    try
                                    {
                                        var dn = k.defName ?? string.Empty;
                                        var lb = k.label ?? string.Empty;
                                        if (dn.IndexOf("Shambler", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                                        if (dn.IndexOf("Revenant", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                                        if (lb.IndexOf("Shambler", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                                        if (lb.IndexOf("Revenant", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                                        if (lb.IndexOf("蹒跚", StringComparison.OrdinalIgnoreCase) >= 0) return false; // CN label hint
                                    }
                                    catch { }
                                    return true;
                                })
                                .ToList();
                            if (insectKinds != null && insectKinds.Count > 0)
                            {
                                foreach (var k in insectKinds.Take(8)) list.Add(k);
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
                                // 保险：过滤掉任何人形单位
                                try { if (kind.race?.race?.Humanlike == true) continue; } catch { }
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
