using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class WildlifeOpportunitiesExecutor : IToolExecutor
    {
        public string Name => "get_wildlife_opportunities";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct)
        {
            // 无参数 v1
            IWorldDataService wds = null;
            try { wds = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (wds == null) return new { ok = false };
            var snap = await wds.GetWildlifeOpportunitiesAsync(ct).ConfigureAwait(false);
            var items = (snap?.Species ?? new List<WildlifeSpeciesGroup>()).Select(s => new Dictionary<string, object>
            {
                ["species"] = s.Species,
                ["defName"] = s.DefName,
                ["count"] = s.Count,
                ["predator"] = s.Predator,
                ["herdAnimal"] = s.HerdAnimal,
                ["packAnimal"] = s.PackAnimal,
                ["insect"] = s.IsInsect,
                ["explosive"] = s.Explosive,
                ["manhunterOnDamageChance"] = s.ManhunterOnDamageChance,
                ["avgBodySize"] = s.AvgBodySize,
                ["avgWildness"] = s.AvgWildness,
                ["meatPer"] = s.MeatPer,
                ["leatherPer"] = s.LeatherPer,
                ["leatherDef"] = s.LeatherDef,
                ["totalMeat"] = s.TotalMeat,
                ["totalLeather"] = s.TotalLeather,
                ["seasonOk"] = s.SeasonOk,
                ["suggestedApproach"] = s.SuggestedApproach,
                ["notes"] = s.Notes ?? new string[0]
            }).ToList();

            return new Dictionary<string, object>
            {
                ["items"] = items,
                ["note"] = snap?.Note ?? ""
            };
        }
    }
}
