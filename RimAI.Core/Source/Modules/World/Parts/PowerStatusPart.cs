using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
	internal sealed class PowerStatusPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public PowerStatusPart(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("PowerStatusPart requires ConfigurationService");
		}

		public Task<PowerStatusSnapshot> GetAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap ?? throw new WorldDataException("No current map");
				var nets = map.powerNetManager?.AllNetsListForReading;
				if (nets == null) return new PowerStatusSnapshot { GenW = 0, ConsW = 0, NetW = 0, Batteries = new BatteryStatus { Count = 0, StoredWd = 0, CapacityWd = 0, DaysLeft = -1 } };

				float genW = 0f, consW = 0f;
				int batCount = 0;
				float storedWd = 0f, capWd = 0f;

				foreach (var net in nets)
				{
					if (net == null) continue;
					// 发电/用电：遍历 powerComps（开机状态）
					for (int i = 0; i < net.powerComps.Count; i++)
					{
						var pc = net.powerComps[i];
						if (pc == null || !pc.PowerOn) continue;
						var w = pc.PowerOutput; // W（正=发电，负=用电/待机）
						if (w > 0) genW += w; else consW += -w;
					}
					// 电池统计
					for (int j = 0; j < net.batteryComps.Count; j++)
					{
						var bat = net.batteryComps[j];
						if (bat == null) continue;
						batCount++;
						if (!bat.StunnedByEMP)
						{
							storedWd += bat.StoredEnergy; // W-days
						}
						capWd += bat.Props.storedEnergyMax; // W-days
					}
				}

				float netW = genW - consW;
				float daysLeft = -1f; // 不适用/正在充电
				if (netW < 0 && storedWd > 0)
				{
					// 将净耗电（W）转换为 W-days/天：一天内消耗 = |netW| * WattsToWattDaysPerTick * 60k ticks
					// 60,000 ticks/day * 1.6666667e-5 = 1.0 Wd/W/day
					float dailyWd = -netW * CompPower.WattsToWattDaysPerTick * 60000f;
					daysLeft = dailyWd > 0 ? (storedWd / dailyWd) : -1f;
				}

				return new PowerStatusSnapshot
				{
					GenW = genW,
					ConsW = consW,
					NetW = netW,
					Batteries = new BatteryStatus { Count = batCount, StoredWd = storedWd, CapacityWd = capWd, DaysLeft = daysLeft }
				};
			}, name: "PowerStatusPart.Get", ct: cts.Token);
		}
	}
}
