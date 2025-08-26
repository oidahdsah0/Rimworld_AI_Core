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
    // v1：近似计算殖民地安防态势（炮塔/陷阱/覆盖/盲区）
    internal sealed class SecurityPosturePart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;

        public SecurityPosturePart(ISchedulerService scheduler, ConfigurationService cfg)
        {
            _scheduler = scheduler;
            _cfg = cfg ?? throw new InvalidOperationException("SecurityPosturePart requires ConfigurationService");
        }

        public Task<SecurityPostureSnapshot> GetAsync(CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap ?? throw new WorldDataException("No current map");

                // 1) 领地集合（优先家园区）
                var territory = BuildTerritory(map);
                if (territory.Count == 0)
                {
                    // 无领地时，给一个小的核心区域以避免除零
                    var center = map.Center;
                    territory.Add(center);
                    foreach (var c in GenRadial.RadialCellsAround(center, 6f, useCenter: true))
                    {
                        if (c.InBounds(map)) territory.Add(c);
                    }
                }
                // 基于“边缘±15”构造稀疏评估集（初步，后续还会并入炮塔周边采样）
                var eval = BuildEdgeBandSparse(map, territory, bandRadius: 15, stride: 3);

                // 2) 炮塔+陷阱 单次扫描（兼容 Mod）
                var allColonistBuildings = map.listerBuildings?.allBuildingsColonist ?? new List<Building>();
                var turrets = new List<SecurityTurretItem>();
                var traps = new List<SecurityTrapItem>();
                foreach (var b in allColonistBuildings)
                {
                    try
                    {
                        if (b == null || b.Destroyed) continue;
                        // 判定炮塔
                        bool isTurret = false;
                        try { isTurret = b.def?.building?.IsTurret == true; } catch { }
                        if (!isTurret && b is Building_Turret) isTurret = true;
                        if (!isTurret)
                        {
                            var mannable = b.TryGetComp<CompMannable>();
                            if (mannable != null) isTurret = true;
                        }
                        if (isTurret)
                        {
                            float range = 25f, minRange = 0f; bool los = true, overhead = false; float dps = 0f;
                            bool manned = false, holdFire = false, powered = true;
                            try { var p = b.TryGetComp<CompPowerTrader>(); if (p != null) powered = p.PowerOn; } catch { }
                            try { var mann = b.TryGetComp<CompMannable>(); if (mann != null) manned = mann.MannedNow; } catch { }
                            try
                            {
                                if (b is Building_TurretGun tg)
                                {
                                    var verb = tg.GunCompEq?.PrimaryVerb ?? tg.AttackVerb;
                                    var vp = verb?.verbProps;
                                    if (vp != null)
                                    {
                                        range = vp.range;
                                        minRange = vp.minRange;
                                        los = vp.requireLineOfSight;
                                        try { overhead = vp.defaultProjectile?.projectile?.flyOverhead ?? false; } catch { }
                                        float dmg = 0f; int burst = Math.Max(1, vp.burstShotCount);
                                        try { dmg = vp.defaultProjectile?.projectile?.GetDamageAmount(null, null) ?? 0f; } catch { }
                                        float warm = Math.Max(0.1f, vp.warmupTime);
                                        float cool = Math.Max(0.1f, vp.defaultCooldownTime);
                                        dps = (dmg * burst) / (warm + cool);
                                    }
                                    // 反射读取 HoldFire（不同版本/Mod 可能无此属性）
                                    try
                                    {
                                        var pi = tg.GetType().GetProperty("HoldFire");
                                        if (pi != null && pi.PropertyType == typeof(bool)) holdFire = (bool)(pi.GetValue(tg) ?? false);
                                        else
                                        {
                                            var fi = tg.GetType().GetField("holdFire", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                                            if (fi != null && fi.FieldType == typeof(bool)) holdFire = (bool)(fi.GetValue(tg) ?? false);
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            turrets.Add(new SecurityTurretItem
                            {
                                Type = b.def?.defName ?? b.GetType().Name,
                                Label = SafeLabel(b),
                                X = b.Position.x,
                                Z = b.Position.z,
                                Range = range,
                                MinRange = minRange,
                                LosRequired = los,
                                FlyOverhead = overhead,
                                DpsScore = dps,
                                Powered = powered,
                                Manned = manned,
                                HoldFire = holdFire
                            });
                        }
                        // 判定陷阱
                        bool isTrap = b is Building_Trap;
                        if (!isTrap)
                        {
                            string dn = b.def?.defName?.ToLowerInvariant() ?? string.Empty;
                            if (dn.Contains("trap")) isTrap = true;
                        }
                        if (isTrap)
                        {
                            bool resettable = true;
                            try { resettable = b.GetComps<ThingComp>().Any(c => c.GetType().Name.Contains("Rearm", StringComparison.OrdinalIgnoreCase)); } catch { }
                            traps.Add(new SecurityTrapItem
                            {
                                Type = b.def?.defName ?? b.GetType().Name,
                                Label = SafeLabel(b),
                                X = b.Position.x,
                                Z = b.Position.z,
                                Resettable = resettable
                            });
                        }
                    }
                    catch { }
                }

                // 在炮塔周边补充稀疏采样（玩家常在领地内布置防线）
                ExpandEvalWithTurretDonuts(map, eval, turrets, stride: 3);
                // 陷阱位置哈希，路径抽样 O(1) 查询
                var trapSet = new HashSet<IntVec3>();
                foreach (var tr in traps)
                {
                    trapSet.Add(new IntVec3(tr.X, 0, tr.Z));
                }

                // 4) 覆盖栈图（对评估集每格计算可命中炮塔数）
                var coverCounts = new Dictionary<IntVec3, int>();
                var overheadCovered = new HashSet<IntVec3>();
                var turretCalcs = PrepareTurretCalcs(turrets);
                foreach (var cell in eval)
                {
                    int stack = 0; bool overheadAny = false;
                    foreach (var t in turretCalcs)
                    {
                        try
                        {
                            int dx = t.Pos.x - cell.x; int dz = t.Pos.z - cell.z;
                            int distSq = dx * dx + dz * dz;
                            if (distSq > t.RangeSq || distSq < t.MinRangeSq) continue;
                            bool canHit = true;
                            if (t.LosRequired)
                            {
                                if (!GenSight.LineOfSight(t.Pos, cell, map, skipFirstCell: true)) canHit = false;
                            }
                            if (!canHit) continue;
                            stack++;
                            if (t.FlyOverhead) overheadAny = true;
                        }
                        catch { }
                    }
                    if (stack > 0) coverCounts[cell] = stack;
                    if (overheadAny) overheadCovered.Add(cell);
                }

                float total = Math.Max(1, eval.Count);
                float covered = coverCounts.Count;
                float strong = coverCounts.Values.Count(v => v >= 2);
                float avgStack = coverCounts.Count > 0 ? (float)coverCounts.Values.Average() : 0f;
                float overheadPct = (overheadCovered.Count > 0) ? ((float)overheadCovered.Count / total) : 0f;

                // 5) 入口路径抽样（8 个方位，直线抽样）
                var approaches = SampleApproaches(map, territory, coverCounts, trapSet);

                // 6) 盲区聚类（在评估集上寻找未覆盖连通块 Top3）
                var gaps = FindCoverageGaps(map, eval, coverCounts, territoryCenter: AverageCell(territory));

                string note = null;
                if (turrets.Count == 0)
                {
                    note = "No turrets found; metrics are approximate based on traps and territory only.";
                }

                return new SecurityPostureSnapshot
                {
                    Turrets = turrets,
                    Traps = traps,
                    Coverage = new SecurityCoverageInfo
                    {
                        AreaPct = covered / total,
                        StrongPct = strong / total,
                        AvgStack = avgStack,
                        OverheadPct = overheadPct,
                        Approaches = approaches
                    },
                    Gaps = gaps,
                    Note = note
                };
            }, name: "SecurityPosturePart.Get", ct: cts.Token);
        }

        private static string SafeLabel(Thing t)
        {
            try { var s = t.LabelCap.ToString(); if (!string.IsNullOrWhiteSpace(s)) return s; } catch { }
            try { var s2 = t.Label; if (!string.IsNullOrWhiteSpace(s2)) return s2; } catch { }
            try { return t.def?.label ?? t.def?.defName ?? t.GetType().Name; } catch { }
            return t?.GetType().Name ?? string.Empty;
        }

        private static HashSet<IntVec3> BuildTerritory(Map map)
        {
            var set = new HashSet<IntVec3>();
            try
            {
                var home = map.areaManager?.Home;
                if (home != null && home.TrueCount > 0)
                {
                    foreach (var c in home.ActiveCells) if (c.InBounds(map)) set.Add(c);
                    return set;
                }
            }
            catch { }

            // 回退：以殖民者建筑为核心扩张一定半径
            try
            {
                var buildings = map.listerBuildings?.allBuildingsColonist ?? new List<Building>();
                foreach (var b in buildings)
                {
                    if (b == null || b.Destroyed) continue;
                    var rect = b.OccupiedRect();
                    rect = rect.ExpandedBy(6);
                    rect.ClipInsideMap(map);
                    foreach (var c in rect.Cells) set.Add(c);
                }
            }
            catch { }
            return set;
        }

    private static List<SecurityApproachItem> SampleApproaches(Map map, HashSet<IntVec3> territory, Dictionary<IntVec3, int> coverCounts, HashSet<IntVec3> trapSet)
        {
            var list = new List<SecurityApproachItem>();
            if (territory.Count == 0) return list;
            var center = AverageCell(territory);
            var edges = new List<IntVec3>
            {
                new IntVec3(map.Center.x, 0, 0),
                new IntVec3(map.Center.x, 0, map.Size.z-1),
                new IntVec3(0, 0, map.Center.z),
                new IntVec3(map.Size.x-1, 0, map.Center.z),
                new IntVec3(0,0,0),
                new IntVec3(map.Size.x-1,0,0),
                new IntVec3(0,0,map.Size.z-1),
                new IntVec3(map.Size.x-1,0,map.Size.z-1)
            };
            foreach (var entry in edges)
            {
                var path = SampleLine(entry, center, map, maxSteps: 256);
                if (path.Count == 0) continue;
                int inside = 0; int zeroSeq = 0; int maxZero = 0; float sum = 0f; int trapHits = 0;
                foreach (var c in path)
                {
                    if (!territory.Contains(c)) continue;
                    inside++;
                    int stack = 0; if (coverCounts.TryGetValue(c, out var v)) stack = v;
                    sum += stack;
                    if (stack == 0) { zeroSeq++; if (zeroSeq > maxZero) maxZero = zeroSeq; }
                    else zeroSeq = 0;
                    if (trapSet.Contains(c)) trapHits++;
                }
                if (inside == 0) continue;
                list.Add(new SecurityApproachItem
                {
                    EntryX = entry.x,
                    EntryZ = entry.z,
                    AvgFire = sum / inside,
                    MaxGapLen = maxZero,
                    TrapDensity = (float)trapHits / inside
                });
            }
            return list;
        }

        private static IntVec3 AverageCell(HashSet<IntVec3> cells)
        {
            long sx = 0, sz = 0; int n = 0;
            foreach (var c in cells) { sx += c.x; sz += c.z; n++; }
            if (n <= 0) return IntVec3.Zero;
            return new IntVec3((int)(sx / n), 0, (int)(sz / n));
        }

        private static List<IntVec3> SampleLine(IntVec3 from, IntVec3 to, Map map, int maxSteps)
        {
            var list = new List<IntVec3>();
            int x0 = from.x, z0 = from.z, x1 = to.x, z1 = to.z;
            int dx = Math.Abs(x1 - x0), dz = Math.Abs(z1 - z0);
            int sx = x0 < x1 ? 1 : -1; int sz = z0 < z1 ? 1 : -1;
            int err = dx - dz;
            int steps = 0;
            while (true)
            {
                var c = new IntVec3(x0, 0, z0);
                if (c.InBounds(map)) list.Add(c);
                if (x0 == x1 && z0 == z1) break;
                int e2 = 2 * err;
                if (e2 > -dz) { err -= dz; x0 += sx; }
                if (e2 < dx) { err += dx; z0 += sz; }
                steps++; if (steps >= maxSteps) break;
            }
            return list;
        }

        private static List<SecurityGapItem> FindCoverageGaps(Map map, HashSet<IntVec3> evalSet, Dictionary<IntVec3, int> coverCounts, IntVec3 territoryCenter)
        {
            var gaps = new List<SecurityGapItem>();
            var visited = new HashSet<IntVec3>();
            var zeroCells = new HashSet<IntVec3>(evalSet.Where(c => !coverCounts.ContainsKey(c)));
            var center = territoryCenter;
            foreach (var c in zeroCells)
            {
                if (visited.Contains(c)) continue;
                // BFS 连通块
                var queue = new Queue<IntVec3>();
                var comp = new List<IntVec3>();
                queue.Enqueue(c); visited.Add(c);
                int minX = c.x, maxX = c.x, minZ = c.z, maxZ = c.z;
                while (queue.Count > 0 && comp.Count < 4000)
                {
                    var cur = queue.Dequeue(); comp.Add(cur);
                    if (cur.x < minX) minX = cur.x; if (cur.x > maxX) maxX = cur.x;
                    if (cur.z < minZ) minZ = cur.z; if (cur.z > maxZ) maxZ = cur.z;
                    foreach (var nb in GenAdjFast.AdjacentCellsCardinal(cur))
                    {
                        if (!nb.InBounds(map)) continue;
                        if (!zeroCells.Contains(nb) || visited.Contains(nb)) continue;
                        visited.Add(nb); queue.Enqueue(nb);
                    }
                }
                if (comp.Count < 8) continue; // 过滤极小空隙
                var cc = new IntVec3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
                gaps.Add(new SecurityGapItem
                {
                    CenterX = cc.x,
                    CenterZ = cc.z,
                    MinX = minX,
                    MinZ = minZ,
                    MaxX = maxX,
                    MaxZ = maxZ,
                    Area = comp.Count,
                    DistToCore = (int)(cc - center).LengthHorizontal,
                    Reason = "no_turret_coverage"
                });
            }
            // 取 Top3 按面积
            return gaps.OrderByDescending(g => g.Area).Take(3).ToList();
        }

        // 领地边缘±bandRadius 稀疏采样：用双向 BFS（内外各扩散 bandRadius）避免对每个边界做大半径重复枚举
        private static HashSet<IntVec3> BuildEdgeBandSparse(Map map, HashSet<IntVec3> territory, int bandRadius, int stride)
        {
            var eval = new HashSet<IntVec3>();
            if (territory.Count == 0) return eval;
            var boundary = FindBoundaryCells(map, territory);

            // 内侧 BFS
            var visitedIn = new HashSet<IntVec3>();
            var qIn = new Queue<(IntVec3 cell, int d)>();
            foreach (var b in boundary)
            {
                qIn.Enqueue((b, 0)); visitedIn.Add(b);
            }
            while (qIn.Count > 0)
            {
                var (cur, d) = qIn.Dequeue();
                if (((cur.x + cur.z) % stride) == 0) eval.Add(cur);
                if (d >= bandRadius) continue;
                foreach (var nb in GenAdjFast.AdjacentCellsCardinal(cur))
                {
                    if (!nb.InBounds(map)) continue;
                    if (!territory.Contains(nb)) continue; // 仅内侧
                    if (visitedIn.Contains(nb)) continue;
                    visitedIn.Add(nb);
                    qIn.Enqueue((nb, d + 1));
                }
            }

            // 外侧 BFS：以边界外邻接格为种子
            var visitedOut = new HashSet<IntVec3>();
            var qOut = new Queue<(IntVec3 cell, int d)>();
            foreach (var b in boundary)
            {
                foreach (var nb in GenAdjFast.AdjacentCellsCardinal(b))
                {
                    if (!nb.InBounds(map)) continue;
                    if (territory.Contains(nb)) continue; // 仅外侧
                    if (visitedOut.Contains(nb)) continue;
                    visitedOut.Add(nb);
                    qOut.Enqueue((nb, 0));
                }
            }
            while (qOut.Count > 0)
            {
                var (cur, d) = qOut.Dequeue();
                if (((cur.x + cur.z) % stride) == 0) eval.Add(cur);
                if (d >= bandRadius) continue;
                foreach (var nb in GenAdjFast.AdjacentCellsCardinal(cur))
                {
                    if (!nb.InBounds(map)) continue;
                    if (territory.Contains(nb)) continue; // 仅外侧
                    if (visitedOut.Contains(nb)) continue;
                    visitedOut.Add(nb);
                    qOut.Enqueue((nb, d + 1));
                }
            }

            // 若采样仍过稀，补充部分领地格以稳定统计
            if (eval.Count < 64)
            {
                foreach (var c in territory)
                {
                    if (((c.x + c.z) % stride) != 0) continue;
                    eval.Add(c);
                    if (eval.Count >= 64) break;
                }
            }
            return eval;
        }

        private sealed class TurretCalc
        {
            public IntVec3 Pos;
            public int RangeSq;
            public int MinRangeSq;
            public bool LosRequired;
            public bool FlyOverhead;
        }

        private static List<TurretCalc> PrepareTurretCalcs(List<SecurityTurretItem> turrets)
        {
            var list = new List<TurretCalc>(turrets.Count);
            foreach (var t in turrets)
            {
                try
                {
                    int r = Math.Max(0, (int)Math.Ceiling(t.Range));
                    int rMin = Math.Max(0, (int)Math.Floor(t.MinRange));
                    list.Add(new TurretCalc
                    {
                        Pos = new IntVec3(t.X, 0, t.Z),
                        RangeSq = r * r,
                        MinRangeSq = rMin * rMin,
                        LosRequired = t.LosRequired,
                        FlyOverhead = t.FlyOverhead
                    });
                }
                catch { }
            }
            return list;
        }

        private static List<IntVec3> FindBoundaryCells(Map map, HashSet<IntVec3> territory)
        {
            var list = new List<IntVec3>();
            foreach (var c in territory)
            {
                bool isBoundary = false;
                foreach (var nb in GenAdjFast.AdjacentCellsCardinal(c))
                {
                    if (!nb.InBounds(map) || !territory.Contains(nb)) { isBoundary = true; break; }
                }
                if (isBoundary) list.Add(c);
            }
            return list;
        }

        // 将每个炮塔的射程周边（限制至25格）加入评估集（同样稀疏）
        private static void ExpandEvalWithTurretDonuts(Map map, HashSet<IntVec3> eval, List<SecurityTurretItem> turrets, int stride)
        {
            foreach (var t in turrets)
            {
                var center = new IntVec3(t.X, 0, t.Z);
                int r = Math.Max(6, Math.Min(25, (int)Math.Ceiling(t.Range + 0.5f)));
                foreach (var c in GenRadial.RadialCellsAround(center, r, useCenter: true))
                {
                    if (!c.InBounds(map)) continue;
                    if (((c.x + c.z) % stride) != 0) continue;
                    eval.Add(c);
                }
            }
        }
    }
}
