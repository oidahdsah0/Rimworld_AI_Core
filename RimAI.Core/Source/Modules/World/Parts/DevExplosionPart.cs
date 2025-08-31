using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimWorld;
using Verse;

namespace RimAI.Core.Source.Modules.World.Parts
{
	internal sealed class DevExplosionPart
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public DevExplosionPart(ISchedulerService scheduler, ConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg ?? throw new InvalidOperationException("DevExplosionPart requires ConfigurationService");
		}

		public async Task<int> TryExplodeNearEnemiesAsync(int strikes, int radius, CancellationToken ct = default)
		{
			// 使用配置的发间隔与半径范围
			int n = Math.Max(1, Math.Min(30, strikes));
			var rng = new System.Random(unchecked(Environment.TickCount ^ n));

			var toolCfg = _cfg?.GetToolingConfig()?.Bombardment ?? new CoreConfig.BombardmentSection();
			int interMin = Math.Max(0, toolCfg.StrikeIntervalMinTicks);
			int interMax = Math.Max(interMin, toolCfg.StrikeIntervalMaxTicks);
			int offsetR = Math.Max(0, toolCfg.TargetOffsetRadius);
			int expMin = Math.Max(1, toolCfg.ExplosionRadiusMin);
			int expMax = Math.Max(expMin, toolCfg.ExplosionRadiusMax);

			// 允许的伤害类型白名单（避免过于破坏性的 rare 类型）
			var allowed = new[]
			{
				DamageDefOf.Bomb,
				DamageDefOf.Flame,
				DamageDefOf.EMP,
				DamageDefOf.Stun
			};

			int executed = 0;

			for (int i = 0; i < n; i++)
			{
				// 每次打击都在主线程重新获取地图与敌对列表，保证目标有效性
				int thisStrike = await _scheduler.ScheduleOnMainThreadAsync(() =>
				{
					try
					{
						var map = Find.CurrentMap;
						if (map == null) return -1; // 地图无效：终止序列
						var hostiles = new List<Pawn>();
						foreach (var p in map.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
						{
							if (p == null || p.Dead) continue;
							try { if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) hostiles.Add(p); } catch { }
						}
						if (hostiles.Count == 0) return -1; // 没有敌人：终止序列

						var target = hostiles[rng.Next(hostiles.Count)];
						var center = target.Position;
						IntVec3 cell;
						// 落点：目标敌人半径 offsetR 格内随机（含自身格），优先可站立且未迷雾
						if (!CellFinder.TryFindRandomCellNear(center, map, offsetR, c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map), out cell))
						{
							cell = center;
						}
						var dmg = allowed[rng.Next(allowed.Length)];
						// 杀伤半径：配置范围随机
						float explosionRadius = (float)rng.Next(expMin, expMax + 1);
						try
						{
							GenExplosion.DoExplosion(cell, map, explosionRadius, dmg, instigator: null, damAmount: -1, armorPenetration: -1f, SoundDefOf.Artillery_ShellLoaded);
							return 1;
						}
						catch { return 0; }
					}
					catch { return -1; }
				}, name: "World.DevExplosion.Strike", ct: ct).ConfigureAwait(false);

				if (thisStrike < 0) { break; }
				executed += thisStrike;

				// 打完最后一发就不再等待
				if (i < n - 1)
				{
					int delayTicks = rng.Next(interMin, interMax + 1);
					try { await _scheduler.DelayOnMainThreadAsync(delayTicks, ct).ConfigureAwait(false); } catch { /* 允许取消/忽略 */ }
				}
			}

			return executed;
		}
	}
}


