using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class AlertDigestExecutor : IToolExecutor
    {
        public string Name => "get_alert_digest";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return new { ok = false };
            var s = await world.GetAlertDigestAsync(ct).ConfigureAwait(false);
            var list = (s?.Alerts ?? new List<AlertItem>()).Select(a => new Dictionary<string, object>
            {
                ["id"] = a.Id,
                ["label"] = a.Label,
                ["severity"] = a.Severity,
                ["hint"] = a.Hint
            }).ToList();
            return new Dictionary<string, object> { ["alerts"] = list };
        }
    }
}
