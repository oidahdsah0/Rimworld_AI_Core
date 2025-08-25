using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class ResearchOptionsExecutor : IToolExecutor
    {
        public string Name => "get_research_options";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetResearchOptionsAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                object res = new
                {
                    ok = true,
                    research = new
                    {
                        current = (s?.Current == null) ? null : new
                        {
                            defName = s.Current.DefName,
                            label = s.Current.Label,
                            progressPct = s.Current.ProgressPct,
                            workLeft = s.Current.WorkLeft,
                            etaDays = s.Current.EtaDays
                        },
                        availableNow = (s?.AvailableNow ?? new List<ResearchOptionItem>()).Select(x => new
                        {
                            defName = x.DefName,
                            label = x.Label,
                            desc = x.Desc,
                            baseCost = x.BaseCost,
                            techLevel = x.TechLevel,
                            prereqs = x.Prereqs,
                            benches = x.Benches,
                            techprintsNeeded = x.TechprintsNeeded,
                            roughTimeDays = x.RoughTimeDays
                        }),
                        lockedKey = (s?.LockedKey ?? new List<ResearchLockedItem>()).Select(x => new
                        {
                            defName = x.DefName,
                            label = x.Label,
                            missingPrereqs = x.MissingPrereqs,
                            benchesMissing = x.BenchesMissing,
                            techprintsMissing = x.TechprintsMissing,
                            note = x.Note
                        }),
                        colony = (s?.Colony == null) ? null : new { researchers = s.Colony.Researchers, effectiveSpeed = s.Colony.EffectiveSpeed }
                    }
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
