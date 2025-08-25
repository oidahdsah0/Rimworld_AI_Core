using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
    internal sealed class EnvironmentPart
    {
        private readonly ISchedulerService _scheduler;
        private readonly ConfigurationService _cfg;
        public EnvironmentPart(ISchedulerService scheduler, ConfigurationService cfg)
        { _scheduler = scheduler; _cfg = cfg; }

        public Task<float> GetBeautyAverageAsync(int centerX, int centerZ, int radius, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap; if (map == null) throw new WorldDataException("Map missing");
                int r = System.Math.Max(0, radius); long n = 0; double sum = 0.0;
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = new IntVec3(centerX + dx, 0, centerZ + dz);
                        if (!cell.InBounds(map)) continue;
                        float beauty = 0f; try { beauty = BeautyUtility.CellBeauty(cell, map); } catch { beauty = 0f; }
                        sum += beauty; n++;
                    }
                }
                return (float)(n > 0 ? (sum / n) : 0.0);
            }, name: "GetBeautyAverage", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetTerrainCountsAsync(int centerX, int centerZ, int radius, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                var map = Find.CurrentMap; if (map == null) throw new WorldDataException("Map missing");
                int r = System.Math.Max(0, radius);
                var counting = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = new IntVec3(centerX + dx, 0, centerZ + dz);
                        if (!cell.InBounds(map)) continue;
                        var terr = cell.GetTerrain(map);
                        var key = terr?.label ?? terr?.defName ?? "(unknown)";
                        if (!counting.TryGetValue(key, out var c)) c = 0;
                        counting[key] = c + 1;
                    }
                }
                var list = new System.Collections.Generic.List<TerrainCountItem>(counting.Count);
                foreach (var kv in counting) list.Add(new TerrainCountItem { Terrain = kv.Key, Count = kv.Value });
                return (System.Collections.Generic.IReadOnlyList<TerrainCountItem>)list;
            }, name: "GetTerrainCounts", ct: cts.Token);
        }

        public Task<float> GetPawnBeautyAverageAsync(int pawnLoadId, int radius, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var mapRef1 = pawn.Map; if (mapRef1 == null) throw new WorldDataException("Map missing");
                var center = pawn.Position;
                int r = System.Math.Max(0, radius);
                long n = 0; double sum = 0.0;
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = center + new IntVec3(dx, 0, dz);
                        if (!cell.InBounds(mapRef1)) continue;
                        float beauty = 0f; try { beauty = BeautyUtility.CellBeauty(cell, mapRef1); } catch { beauty = 0f; }
                        sum += beauty; n++;
                    }
                }
                return (float)(n > 0 ? (sum / n) : 0.0);
            }, name: "GetPawnBeautyAverage", ct: cts.Token);
        }

        public Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetPawnTerrainCountsAsync(int pawnLoadId, int radius, CancellationToken ct = default)
        {
            var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                if (Current.Game == null) throw new WorldDataException("World not loaded");
                Pawn pawn = null; foreach (var map in Find.Maps) { foreach (var p in map.mapPawns?.AllPawns ?? System.Linq.Enumerable.Empty<Pawn>()) { if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; } } if (pawn != null) break; }
                if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
                var mapRef2 = pawn.Map; if (mapRef2 == null) throw new WorldDataException("Map missing");
                var center = pawn.Position; int r = System.Math.Max(0, radius);
                var counting = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = center + new IntVec3(dx, 0, dz);
                        if (!cell.InBounds(mapRef2)) continue;
                        var terr = cell.GetTerrain(mapRef2);
                        var key = terr?.label ?? terr?.defName ?? "(unknown)";
                        if (!counting.TryGetValue(key, out var c)) c = 0;
                        counting[key] = c + 1;
                    }
                }
                var list = new System.Collections.Generic.List<TerrainCountItem>(counting.Count);
                foreach (var kv in counting) list.Add(new TerrainCountItem { Terrain = kv.Key, Count = kv.Value });
                return (System.Collections.Generic.IReadOnlyList<TerrainCountItem>)list;
            }, name: "GetPawnTerrainCounts", ct: cts.Token);
        }
    }
}
