using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class SecurityPostureExecutor : IToolExecutor
    {
        public string Name => "get_security_posture";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetSecurityPostureAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                object res = new
                {
                    ok = true,
                    security = new
                    {
                        turrets = (s?.Turrets ?? new List<SecurityTurretItem>()).Select(x => new
                        {
                            type = x.Type,
                            label = x.Label,
                            x = x.X,
                            z = x.Z,
                            range = x.Range,
                            minRange = x.MinRange,
                            losRequired = x.LosRequired,
                            flyOverhead = x.FlyOverhead,
                            dpsScore = x.DpsScore,
                            powered = x.Powered,
                            manned = x.Manned,
                            holdFire = x.HoldFire
                        }),
                        traps = (s?.Traps ?? new List<SecurityTrapItem>()).Select(tr => new { type = tr.Type, label = tr.Label, x = tr.X, z = tr.Z, resettable = tr.Resettable }),
                        coverage = (s?.Coverage == null) ? null : new
                        {
                            areaPct = s.Coverage.AreaPct,
                            strongPct = s.Coverage.StrongPct,
                            avgStack = s.Coverage.AvgStack,
                            overheadPct = s.Coverage.OverheadPct,
                            approaches = (s.Coverage.Approaches ?? new List<SecurityApproachItem>()).Select(a => new { entryX = a.EntryX, entryZ = a.EntryZ, avgFire = a.AvgFire, maxGapLen = a.MaxGapLen, trapDensity = a.TrapDensity })
                        },
                        gaps = (s?.Gaps ?? new List<SecurityGapItem>()).Select(g => new { centerX = g.CenterX, centerZ = g.CenterZ, minX = g.MinX, minZ = g.MinZ, maxX = g.MaxX, maxZ = g.MaxZ, area = g.Area, distToCore = g.DistToCore, reason = g.Reason }),
                        note = s?.Note
                    }
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
