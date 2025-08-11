using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Modules.World;
using RimAI.Core.Settings;
using Verse;

namespace RimAI.Core.Modules.Stage.Scan
{
    /// <summary>
    /// 邻近度扫描（P11-M4）：基于地图上小人的邻近关系，建议二人会话候选。
    /// </summary>
    internal sealed class ProximityScan : IStageScan
    {
        public async Task<IReadOnlyList<ConversationSuggestion>> RunAsync(ScanContext ctx, CancellationToken ct = default)
        {
            var cfg = ctx?.Config?.ProximityScan ?? new StageProximityScanConfig();
            if (!(ctx?.Config?.Scan?.Enabled ?? true)) return Array.Empty<ConversationSuggestion>();
            if (!(cfg?.Enabled ?? true)) return Array.Empty<ConversationSuggestion>();

            // 主线程收集候选 Pawn 列表
            List<Pawn> pawns = await Core.Infrastructure.CoreServices.Locator.Get<Core.Infrastructure.ISchedulerService>()
                .ScheduleOnMainThreadAsync(() => CollectEligiblePawns(cfg));

            if (pawns.Count == 0) return Array.Empty<ConversationSuggestion>();

            // 简化：随机落点一个 Pawn A，再从其近邻挑一个 B
            var rnd = new Random(Environment.TickCount);
            var a = pawns[rnd.Next(pawns.Count)];
            var neighbors = pawns.Where(p => p != a && DistanceApprox(a, p) <= Math.Max(1f, cfg.RangeK)).ToList();
            if (neighbors.Count == 0) return Array.Empty<ConversationSuggestion>();
            var b = neighbors[rnd.Next(neighbors.Count)];

            // 触发判定
            bool trigger = false;
            if (cfg.TriggerMode == StageProximityTriggerMode.Threshold)
            {
                var r = rnd.NextDouble();
                trigger = r > Math.Min(1.0, Math.Max(0.0, cfg.TriggerThreshold));
            }
            else
            {
                var r = rnd.NextDouble();
                trigger = r < Math.Max(0.0, Math.Min(1.0, cfg.ProbabilityP));
            }
            if (!trigger) return Array.Empty<ConversationSuggestion>();

            var pid = ctx.ParticipantId;
            var idA = pid.FromVerseObject(a);
            var idB = pid.FromVerseObject(b);
            var suggestion = new ConversationSuggestion
            {
                Participants = new List<string> { idA, idB },
                Origin = "PawnBehavior",
                InitiatorId = idA,
                Scenario = string.Empty,
                Priority = 0,
                Seed = unchecked(idA.GetHashCode() ^ (idB?.GetHashCode() ?? 0))
            };
            return new[] { suggestion };
        }

        private static List<Pawn> CollectEligiblePawns(StageProximityScanConfig cfg)
        {
            var list = new List<Pawn>();
            if (Find.Maps == null) return list;
            foreach (var map in Find.Maps)
            {
                var pawns = map?.mapPawns?.FreeColonistsSpawned;
                if (pawns == null) continue;
                foreach (var p in pawns)
                {
                    if (p == null || p.Dead || p.Downed) continue;
                    if (cfg.ExcludeBusy && p?.jobs?.curDriver != null) continue;
                    if (cfg.OnlyNonHostile && p.HostileTo(Faction.OfPlayer)) continue;
                    list.Add(p);
                }
            }
            return list;
        }

        private static float DistanceApprox(Pawn a, Pawn b)
        {
            try
            {
                if (a?.Map != b?.Map) return float.MaxValue;
                var da = a.Position; var db = b.Position;
                var dx = da.x - db.x; var dz = da.z - db.z;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
            catch { return float.MaxValue; }
        }
    }
}


