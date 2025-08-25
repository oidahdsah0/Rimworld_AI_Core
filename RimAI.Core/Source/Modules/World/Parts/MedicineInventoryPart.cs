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
	internal sealed class MedicineInventoryPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public MedicineInventoryPart(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("MedicineInventoryPart requires ConfigurationService");
		}

		public Task<MedicineInventorySnapshot> GetAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap ?? throw new WorldDataException("No current map");
				map.resourceCounter?.UpdateResourceCounts();
				var list = new List<MedicineItemInfo>();
				int total = 0;
				var counted = map.resourceCounter?.AllCountedAmounts ?? new Dictionary<ThingDef, int>();
				foreach (var kv in counted)
				{
					var def = kv.Key; var count = kv.Value;
					if (def == null || count <= 0) continue;
					if (!def.IsMedicine) continue;
					float potency = 0f; try { potency = def.statBases?.FirstOrDefault(s => s.stat == StatDefOf.MedicalPotency)?.value ?? 0f; } catch { }
					total += count;
					list.Add(new MedicineItemInfo { DefName = def.defName, Label = def.label ?? def.defName, Count = count, Potency = potency });
				}
				var medsGroup = map.listerThings?.ThingsInGroup(ThingRequestGroup.Medicine) ?? new List<Thing>();
				var extra = medsGroup.GroupBy(t => t.def).Select(g => new { Def = g.Key, Count = g.Sum(t => t.stackCount) });
				foreach (var e in extra)
				{
					if (e.Def == null) continue;
					if (list.Any(x => x.DefName == e.Def.defName)) continue;
					float potency = 0f; try { potency = e.Def.statBases?.FirstOrDefault(s => s.stat == StatDefOf.MedicalPotency)?.value ?? 0f; } catch { }
					total += e.Count;
					list.Add(new MedicineItemInfo { DefName = e.Def.defName, Label = e.Def.label ?? e.Def.defName, Count = e.Count, Potency = potency });
				}
				list = list.OrderByDescending(m => m.Potency).ThenByDescending(m => m.Count).ToList();
				return new MedicineInventorySnapshot { TotalCount = total, Items = list };
			}, name: "MedicineInventoryPart.Get", ct: cts.Token);
		}
	}
}
