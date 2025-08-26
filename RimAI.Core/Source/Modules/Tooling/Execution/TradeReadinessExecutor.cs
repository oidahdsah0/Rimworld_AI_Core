using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class TradeReadinessExecutor : IToolExecutor
    {
        public string Name => "get_trade_readiness";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return new { ok = false };
            var s = await world.GetTradeReadinessAsync(ct).ConfigureAwait(false);
            var goods = (s?.Goods ?? new List<TradeGoodsItem>()).Select(g => new Dictionary<string, object>
            {
                ["defName"] = g.DefName,
                ["label"] = g.Label,
                ["qty"] = g.Qty,
                ["totalValue"] = g.TotalValue
            }).ToList();

            return new Dictionary<string, object>
            {
                ["trade"] = new Dictionary<string, object>
                {
                    ["silver"] = s?.Silver ?? 0,
                    ["beacons"] = new Dictionary<string, object>
                    {
                        ["total"] = s?.Beacons?.Total ?? 0,
                        ["powered"] = s?.Beacons?.Powered ?? 0,
                        ["coverageCells"] = s?.Beacons?.CoverageCells ?? 0,
                        ["inRangeStacks"] = s?.Beacons?.InRangeStacks ?? 0
                    },
                    ["comms"] = new Dictionary<string, object>
                    {
                        ["hasConsole"] = s?.Comms?.HasConsole ?? false,
                        ["usableNow"] = s?.Comms?.UsableNow ?? false
                    },
                    ["goods"] = goods
                }
            };
        }
    }
}
