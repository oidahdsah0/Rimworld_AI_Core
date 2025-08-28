using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
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

		public Task<int> TryExplodeNearEnemiesAsync(int strikes, int radius, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					var map = Find.CurrentMap;
					if (map == null) return 0;
					// 收集敌对单位
					var hostiles = new List<Pawn>();
					foreach (var p in map.mapPawns?.AllPawnsSpawned ?? Enumerable.Empty<Pawn>())
					{
						if (p == null || p.Dead) continue;
						try { if (p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer)) hostiles.Add(p); } catch { }
					}
					if (hostiles.Count == 0) return 0;

					int n = Math.Max(1, Math.Min(30, strikes));
					int r = Math.Max(3, Math.Min(30, radius));
					var rng = new System.Random(unchecked(Environment.TickCount ^ hostiles.Count));

					// 允许的伤害类型白名单（避免过于破坏性的 rare 类型）
					var allowed = new[]
					{
						DamageDefOf.Bomb,
						DamageDefOf.Flame,
						DamageDefOf.Smoke,
						DamageDefOf.EMP,
						DamageDefOf.Stun
					};
					int executed = 0;
					for (int i = 0; i < n; i++)
					{
						// 选择一个敌对目标附近
						var target = hostiles[rng.Next(hostiles.Count)];
						var center = target.Position;
						IntVec3 cell;
						if (!CellFinder.TryFindRandomCellNear(center, map, r, c => c.InBounds(map) && c.Standable(map) && !c.Fogged(map), out cell))
						{
							cell = center;
						}
						var dmg = allowed[rng.Next(allowed.Length)];
						float explosionRadius = 2.9f; // 近似迫击炮弹爆炸半径
						try
						{
							GenExplosion.DoExplosion(cell, map, explosionRadius, dmg, instigator: null, damAmount: -1, armorPenetration: -1f, SoundDefOf.Artillery_ShellLoaded);
							executed++;
						}
						catch { /* 忽略单次失败 */ }
					}
					return executed;
				}
				catch { return 0; }
			}, name: "World.DevExplosions", ct: cts.Token);
		}
	}
}


