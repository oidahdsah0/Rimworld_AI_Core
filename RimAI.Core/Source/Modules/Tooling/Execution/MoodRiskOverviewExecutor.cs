using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class MoodRiskOverviewExecutor : IToolExecutor
    {
        public string Name => "get_mood_risk_overview";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetMoodRiskOverviewAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                object res = new
                {
                    ok = true,
                    mood = new
                    {
                        avgPct = s.AvgPct,
                        minorCount = s.MinorCount,
                        majorCount = s.MajorCount,
                        extremeCount = s.ExtremeCount,
                        nearBreakCount = s.NearBreakCount,
                        topCauses = (s.TopCauses ?? new List<MoodCauseItem>()).Select(c => new { label = c.Label, totalImpact = c.TotalImpact, pawnsAffected = c.PawnsAffected })
                    }
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
