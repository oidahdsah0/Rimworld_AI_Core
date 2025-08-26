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
    internal sealed class WildlifeOpportunitiesPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public WildlifeOpportunitiesPart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg;
        }

        public Task<WildlifeOpportunitiesSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() => BuildSnapshotSafe(), name: "GetWildlifeOpportunities", ct: cts.Token);
        }

        private WildlifeOpportunitiesSnapshot BuildSnapshotSafe()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    return new WildlifeOpportunitiesSnapshot { Species = Array.Empty<WildlifeSpeciesGroup>(), Note = "no_map" };
                }

                // 基于野生标签：动物或野人，且未被俘/非殖民派系
                var pawns = map.mapPawns.AllPawns
                    .Where(p => p != null && p.Spawned && (p.Faction == null || p.Faction == Faction.OfInsects) && p.AnimalOrWildMan() && !p.Position.Fogged(p.Map) && !p.IsPrisonerInPrisonCell())
                    .ToList();

                var groups = new List<WildlifeSpeciesGroup>();
                foreach (var g in pawns.GroupBy(p => p.def))
                {
                    var def = g.Key;
                    if (def == null || def.race == null) continue;
                    int count = g.Count();
                    if (count <= 0) continue;

                    // 物种标签与基础属性
                    string label = def.LabelCap.Resolve();
                    string defName = def.defName;
                    var raceProps = def.race;

                    bool predator = false;
                    bool herd = false;
                    bool pack = false;
                    bool isInsect = false;
                    float manhunter = 0f;
                    float bodySize = 0f;
                    float wildness = 0f;
                    float meatPer = 0f;
                    float leatherPer = 0f;
                    string leatherDef = null;
                    bool explosive = false;
                    bool seasonOk = true;
                    var notes = new List<string>();

                    try { predator = raceProps.predator; } catch { }
                    try { herd = raceProps.herdAnimal; } catch { }
                    try { pack = raceProps.packAnimal; } catch { }
                    try { isInsect = def.race.Insect; } catch { }
                    try { bodySize = raceProps.baseBodySize; } catch { }
                    try { wildness = def.GetStatValueAbstract(StatDefOf.Wildness); } catch { }
                    try { meatPer = def.GetStatValueAbstract(StatDefOf.MeatAmount); } catch { }
                    try { leatherPer = def.GetStatValueAbstract(StatDefOf.LeatherAmount); } catch { }
                    try { leatherDef = raceProps.leatherDef?.defName; } catch { }

                    // 复仇几率（受伤）
                    try { manhunter = PawnUtility.GetManhunterOnDamageChance(def); } catch { manhunter = 0f; }

                    // 爆炸性：通过 Hediff 或 Comp 标记不稳定，简化检测（常见：爆炸羊、爆炸鼠）
                    try
                    {
                        // 简化：Boom* 用名判定；或 RaceProps 的特殊 hediff comps 不易直接查，v1 用前缀即可
                        var name = def.defName ?? string.Empty;
                        if (name.IndexOf("Boom", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            explosive = true;
                        }
                    }
                    catch { }

                    // 季节是否合适（是否当前季节可自然出现）
                    try { seasonOk = map.mapTemperature.SeasonAcceptableFor(def); } catch { seasonOk = true; }

                    var group = new WildlifeSpeciesGroup
                    {
                        Species = label,
                        DefName = defName,
                        Count = count,
                        Predator = predator,
                        HerdAnimal = herd,
                        PackAnimal = pack,
                        IsInsect = isInsect,
                        Explosive = explosive,
                        ManhunterOnDamageChance = manhunter,
                        AvgBodySize = bodySize,
                        AvgWildness = wildness,
                        MeatPer = meatPer,
                        LeatherPer = leatherPer,
                        LeatherDef = leatherDef,
                        TotalMeat = meatPer * count,
                        TotalLeather = leatherPer * count,
                        SeasonOk = seasonOk,
                        SuggestedApproach = SuggestApproach(predator, herd, pack, isInsect, explosive, manhunter, bodySize),
                        Notes = notes.ToArray()
                    };

                    // 提示：
                    if (explosive)
                    {
                        notes.Add("死亡爆炸，远程击杀并远离");
                    }
                    if (predator)
                    {
                        notes.Add("捕食者，可能主动攻击");
                    }
                    if (herd)
                    {
                        notes.Add("群居，可能群体复仇");
                    }
                    if (manhunter >= 0.2f)
                    {
                        notes.Add($"复仇几率较高({manhunter:P0})");
                    }

                    groups.Add(group);
                }

                // 简单排序：按总肉量降序，其次复仇几率
                groups = groups
                    .OrderByDescending(g => g.TotalMeat)
                    .ThenByDescending(g => g.ManhunterOnDamageChance)
                    .ToList();

                return new WildlifeOpportunitiesSnapshot
                {
                    Species = groups,
                    Note = "v1"
                };
            }
            catch (Exception ex)
            {
                try { Verse.Log.Warning($"[RimAI.Core] Wildlife opportunities failed: {ex.Message}"); } catch { }
                return new WildlifeOpportunitiesSnapshot { Species = Array.Empty<WildlifeSpeciesGroup>(), Note = "error" };
            }
        }

        private static string SuggestApproach(bool predator, bool herd, bool pack, bool insect, bool explosive, float manhunter, float bodySize)
        {
            if (explosive) return "远程击杀，避免近战";
            if (predator && manhunter > 0.15f) return "远程优先，注意风筝";
            if (herd && manhunter > 0.1f) return "分散击杀，避免集中近战";
            if (insect) return "谨慎接战，优先火力压制";
            if (bodySize >= 1.5f) return "重型目标，建议多人远程";
            return "常规狩猎";
        }
    }
}
