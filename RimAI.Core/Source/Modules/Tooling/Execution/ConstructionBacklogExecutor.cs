using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class ConstructionBacklogExecutor : IToolExecutor
    {
        public string Name => "get_construction_backlog";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetConstructionBacklogAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                object res = new
                {
                    ok = true,
                    builds = (s?.Builds ?? new List<ConstructionBuildItem>()).Select(b => new
                    {
                        thing = b.Thing,
                        count = b.Count,
                        missing = (b.Missing ?? new List<ConstructionMissingItem>()).Select(m => new { res = m.Res, qty = m.Qty })
                    })
                };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
