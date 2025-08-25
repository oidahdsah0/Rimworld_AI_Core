using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class StorageSaturationExecutor : IToolExecutor
    {
        public string Name => "get_storage_saturation";

        public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return Task.FromResult<object>(new { ok = false });
            return world.GetStorageSaturationAsync(ct).ContinueWith<Task<object>>(t =>
            {
                if (t.IsFaulted || t.IsCanceled) return Task.FromResult<object>(new { ok = false });
                var s = t.Result;
                var items = s?.Storages?.Select(x => new { name = x?.Name, usedPct = x?.UsedPct ?? 0f, critical = x?.Critical ?? false, notes = x?.Notes })?.ToArray() ?? new object[0];
                object res = new { ok = true, storages = items };
                return Task.FromResult(res);
            }).Unwrap();
        }
    }
}
