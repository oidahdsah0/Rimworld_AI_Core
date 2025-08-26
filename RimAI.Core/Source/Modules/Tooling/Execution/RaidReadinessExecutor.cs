using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class RaidReadinessExecutor : IToolExecutor
    {
        public string Name => "get_raid_readiness";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
        {
            var world = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<IWorldDataService>();
            var snap = await world.GetRaidReadinessAsync(ct);
            var result = new
            {
                wealth = snap?.Wealth,
                colony = snap?.Colony,
                animals = snap?.Animals,
                mechs = snap?.Mechs,
                points = snap?.Points,
                riskBand = snap?.RiskBand,
                sizes = snap?.SizeEstimates
            };
            return JsonConvert.SerializeObject(result);
        }
    }
}
