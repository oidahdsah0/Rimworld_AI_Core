using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
	internal sealed class ResourceOverviewPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public ResourceOverviewPart(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("ResourceOverviewPart requires ConfigurationService");
		}

		public Task<ResourceOverviewSnapshot> GetAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Verse.Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Verse.Find.CurrentMap ?? throw new WorldDataException("No current map");
				map.resourceCounter?.UpdateResourceCounts();
				var counted = map.resourceCounter?.AllCountedAmounts ?? new Dictionary<ThingDef, int>();
				int colonists = 0; try { colonists = map.mapPawns?.FreeColonistsCount ?? 0; } catch { colonists = 0; }

				// 估算每日营养需求
				float dailyNutritionNeed = colonists > 0 ? colonists * 2.0f : 0f;
				var list = new List<ResourceItem>();
				foreach (var kv in counted)
				{
					var def = kv.Key; var count = kv.Value;
					if (def == null || count <= 0) continue;
					float dailyUse = 0f;
					// 食物：根据每日营养需求折算件耗
					if (def.IsNutritionGivingIngestible && def.ingestible?.HumanEdible == true && dailyNutritionNeed > 0)
					{
						float nutPer = 0f; try { nutPer = def.GetStatValueAbstract(StatDefOf.Nutrition); } catch { }
						if (nutPer > 0f) dailyUse = dailyNutritionNeed / nutPer;
					}
					// 药品：按（殖民者数 * 0.05 瓶/天）粗估
					else if (def.IsMedicine && colonists > 0)
					{
						dailyUse = Math.Max(0.1f, colonists * 0.05f);
					}
					float daysLeft = (dailyUse > 0f) ? (count / dailyUse) : -1f;
					var label = def.label ?? def.defName;
					list.Add(new ResourceItem { DefName = def.defName, Label = label, Count = count, DailyUse = dailyUse, DaysLeft = daysLeft });
				}
				list.Sort((a, b) =>
				{
					int cmp;
					if (a.DaysLeft < 0 && b.DaysLeft < 0) cmp = 0;
					else if (a.DaysLeft < 0) cmp = 1;
					else if (b.DaysLeft < 0) cmp = -1;
					else cmp = a.DaysLeft.CompareTo(b.DaysLeft);
					if (cmp != 0) return cmp;
					return -a.Count.CompareTo(b.Count);
				});
				return new ResourceOverviewSnapshot { Resources = list };
			}, name: "ResourceOverviewPart.Get", ct: cts.Token);
		}
	}
}
