using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimWorld;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
	internal sealed class FoodInventoryPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public FoodInventoryPart(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("FoodInventoryPart requires ConfigurationService");
		}

		public Task<FoodInventorySnapshot> GetAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap ?? throw new WorldDataException("No current map");
				map.resourceCounter?.UpdateResourceCounts();
				var items = new List<FoodItemInfo>();
				float total = 0f;
				var all = map.resourceCounter?.AllCountedAmounts ?? new Dictionary<ThingDef, int>();
				foreach (var kv in all)
				{
					var def = kv.Key; var count = kv.Value;
					if (def == null || count <= 0) continue;
					if (!(def.IsNutritionGivingIngestible && def.ingestible?.HumanEdible == true)) continue;
					float nutPer = 0f; try { nutPer = def.GetStatValueAbstract(StatDefOf.Nutrition); } catch { }
					var sum = nutPer * count; total += sum;
					string pref = string.Empty; try { pref = def.ingestible?.preferability.ToString(); } catch { }
					items.Add(new FoodItemInfo { DefName = def.defName, Label = def.label ?? def.defName, Count = count, NutritionPer = nutPer, TotalNutrition = sum, Preferability = pref });
				}
				items = items.OrderByDescending(i => i.TotalNutrition).ToList();
				return new FoodInventorySnapshot { TotalNutrition = total, Items = items };
			}, name: "FoodInventoryPart.Get", ct: cts.Token);
		}
	}
}
