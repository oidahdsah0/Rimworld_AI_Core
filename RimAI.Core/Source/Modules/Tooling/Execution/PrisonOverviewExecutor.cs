using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class PrisonOverviewExecutor : IToolExecutor
    {
        public string Name => "get_prison_overview";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return new { ok = false };
            var s = await world.GetPrisonOverviewAsync(ct).ConfigureAwait(false);

            var rec = (s?.Recruitables ?? new List<PrisonerRecruitItem>()).Select(r => new Dictionary<string, object>
            {
                ["pawn"] = r.Pawn,
                ["pawnLoadId"] = r.PawnLoadId,
                ["mode"] = r.Mode
            }).ToList();

            return new Dictionary<string, object>
            {
                ["prison"] = new Dictionary<string, object>
                {
                    ["count"] = s?.Count ?? 0,
                    ["recruitables"] = rec,
                    ["risks"] = s?.Risks ?? new List<string>()
                }
            };
        }
    }
}
