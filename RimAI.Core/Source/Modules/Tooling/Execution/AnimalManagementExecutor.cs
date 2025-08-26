using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    internal sealed class AnimalManagementExecutor : IToolExecutor
    {
        public string Name => "get_animal_management";

        public async Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
        {
            IWorldDataService world = null;
            try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
            if (world == null) return new { ok = false };
            var s = await world.GetAnimalManagementAsync(ct).ConfigureAwait(false);

            var species = (s?.Counts?.Species ?? new List<SpeciesCountItem>()).Select(sp => new Dictionary<string, object>
            {
                ["defName"] = sp.DefName,
                ["label"] = sp.Label,
                ["count"] = sp.Count
            }).ToList();

            var sources = (s?.Food?.Sources ?? new List<FoodSourceItem>()).Select(fs => new Dictionary<string, object>
            {
                ["defName"] = fs.DefName,
                ["label"] = fs.Label,
                ["count"] = fs.Count,
                ["nutritionPer"] = fs.NutritionPer,
                ["totalNutrition"] = fs.TotalNutrition
            }).ToList();

            return new Dictionary<string, object>
            {
                ["animals"] = new Dictionary<string, object>
                {
                    ["counts"] = new Dictionary<string, object>
                    {
                        ["total"] = s?.Counts?.Total ?? 0,
                        ["species"] = species
                    },
                    ["training"] = new Dictionary<string, object>
                    {
                        ["obedience"] = new Dictionary<string, object>{{"eligible", s?.Training?.Obedience?.Eligible ?? 0},{"learned", s?.Training?.Obedience?.Learned ?? 0}},
                        ["release"] = new Dictionary<string, object>{{"eligible", s?.Training?.Release?.Eligible ?? 0},{"learned", s?.Training?.Release?.Learned ?? 0}},
                        ["rescue"] = new Dictionary<string, object>{{"eligible", s?.Training?.Rescue?.Eligible ?? 0},{"learned", s?.Training?.Rescue?.Learned ?? 0}},
                        ["haul"] = new Dictionary<string, object>{{"eligible", s?.Training?.Haul?.Eligible ?? 0},{"learned", s?.Training?.Haul?.Learned ?? 0}}
                    },
                    ["food"] = new Dictionary<string, object>
                    {
                        ["totalNutrition"] = s?.Food?.TotalNutrition ?? 0f,
                        ["dailyNeed"] = s?.Food?.DailyNeed ?? 0f,
                        ["days"] = s?.Food?.Days ?? 0f,
                        ["sources"] = sources
                    }
                }
            };
        }
    }
}
