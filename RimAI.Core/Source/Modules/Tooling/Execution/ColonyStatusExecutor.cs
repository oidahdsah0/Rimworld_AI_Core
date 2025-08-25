using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Tooling.Execution
{
	internal sealed class ColonyStatusExecutor : IToolExecutor
	{
		public string Name => "get_colony_status";

		public Task<object> ExecuteAsync(Dictionary<string, object> args, CancellationToken ct = default)
		{
			IWorldDataService world = null;
			try { world = (IWorldDataService)RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve(typeof(IWorldDataService)); } catch { }
			if (world == null)
			{
				return Task.FromResult<object>(new { ok = false, error = "world_service_unavailable" });
			}
			var colonyTask = world.GetColonySnapshotAsync(null, ct);
			var foodTask = world.GetFoodInventoryAsync(ct);
			var medTask = world.GetMedicineInventoryAsync(ct);
			var threatTask = world.GetThreatSnapshotAsync(ct);
			return Task.WhenAll(colonyTask, foodTask, medTask, threatTask).ContinueWith<Task<object>>(t =>
			{
				if (t.IsFaulted || t.IsCanceled)
				{
					return Task.FromResult<object>(new { ok = false });
				}
				var colony = colonyTask.Result;
				var food = foodTask.Result;
				var med = medTask.Result;
				var threat = threatTask.Result;
				object res = new
				{
					ok = true,
					people = new
					{
						count = colony?.ColonistCount ?? 0,
						list = (colony?.Colonists ?? new List<ColonistRecord>())
							.Select(c => new { name = c?.Name ?? string.Empty, title = c?.JobTitle ?? string.Empty, gender = c?.Gender ?? string.Empty, age = c?.Age ?? 0 })
					}
					,
					food = new
					{
						total_nutrition = food?.TotalNutrition ?? 0f,
						items = (food?.Items ?? new List<FoodItemInfo>())
							.Select(i => new { def = i?.DefName ?? string.Empty, label = i?.Label ?? string.Empty, count = i?.Count ?? 0, nutrition_per = i?.NutritionPer ?? 0f, total = i?.TotalNutrition ?? 0f, prefer = i?.Preferability ?? string.Empty })
					}
					,
					medicine = new
					{
						total_count = med?.TotalCount ?? 0,
						items = (med?.Items ?? new List<MedicineItemInfo>())
							.Select(m => new { def = m?.DefName ?? string.Empty, label = m?.Label ?? string.Empty, count = m?.Count ?? 0, potency = m?.Potency ?? 0f })
					}
					,
					threats = new
					{
						hostiles = threat?.HostilePawns ?? 0,
						manhunters = threat?.Manhunters ?? 0,
						mechanoids = threat?.Mechanoids ?? 0,
						threat_points = threat?.ThreatPoints ?? 0f,
						danger = threat?.DangerRating ?? string.Empty,
						fire_danger = threat?.FireDanger ?? 0f,
						last_big_threat_days = threat?.LastBigThreatDaysAgo ?? 0f
					}
				};
				return Task.FromResult(res);
			}).Unwrap();
		}
	}
}
