using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using RimWorld;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using Verse;
using UnityEngine;

namespace RimAI.Core.Source.Modules.World
{
	internal sealed class WorldDataService : IWorldDataService
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

		public WorldDataService(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("WorldDataService requires ConfigurationService");
		}

		public Task<string> GetPlayerNameAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var name = Faction.OfPlayer?.Name ?? "Player";
				return name;
			}, name: "GetPlayerName", ct: cts.Token);
		}

		public Task<System.Collections.Generic.IReadOnlyList<(string serverAId, string serverBId)>> GetAlphaFiberLinksAsync(CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				// 最小占位：返回固定对
				return (System.Collections.Generic.IReadOnlyList<(string, string)>)new (string, string)[] { ("thing:serverA", "thing:serverB") };
			}, name: "GetAlphaFiberLinks", ct: cts.Token);
		}

		public Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				// 最小占位：返回空快照
				return new AiServerSnapshot { ServerId = serverId, TemperatureC = 37, LoadPercent = 50, PowerOn = true, HasAlarm = false };
			}, name: "GetAiServerSnapshot", ct: cts.Token);
		}

		public Task<PawnHealthSnapshot> GetPawnHealthSnapshotAsync(int pawnLoadId, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				Pawn pawn = null;
				foreach (var map in Find.Maps)
				{
					foreach (var p in map.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
					{
						if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; }
					}
					if (pawn != null) break;
				}
				if (pawn == null)
				{
					throw new WorldDataException($"Pawn not found: {pawnLoadId}");
				}
				var dead = pawn.Dead;
				float Lv(PawnCapacityDef def)
				{
					try { return Mathf.Clamp01(pawn.health.capacities.GetLevel(def)); } catch { return 0f; }
				}
				var eatingDef = DefDatabase<PawnCapacityDef>.GetNamedSilentFail("Eating");
				var snap = new PawnHealthSnapshot
				{
					PawnLoadId = pawnLoadId,
					Consciousness = Lv(PawnCapacityDefOf.Consciousness),
					Moving = Lv(PawnCapacityDefOf.Moving),
					Manipulation = Lv(PawnCapacityDefOf.Manipulation),
					Sight = Lv(PawnCapacityDefOf.Sight),
					Hearing = Lv(PawnCapacityDefOf.Hearing),
					Talking = Lv(PawnCapacityDefOf.Talking),
					Breathing = Lv(PawnCapacityDefOf.Breathing),
					BloodPumping = Lv(PawnCapacityDefOf.BloodPumping),
					BloodFiltration = Lv(PawnCapacityDefOf.BloodFiltration),
					Metabolism = eatingDef != null ? Lv(eatingDef) : 0f,
					IsDead = dead
				};
				// 平均数计算交由 Tool 层完成；此处仅提供原始能力值与死亡状态
				snap.AveragePercent = 0f;
				return snap;
			}, name: "GetPawnHealthSnapshot", ct: cts.Token);
		}
	}

	internal sealed class WorldDataException : Exception
	{
		public WorldDataException(string message) : base(message) { }
		public WorldDataException(string message, Exception inner) : base(message, inner) { }
	}
}


