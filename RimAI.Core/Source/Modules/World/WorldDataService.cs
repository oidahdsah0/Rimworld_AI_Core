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

		public Task<PawnPromptSnapshot> GetPawnPromptSnapshotAsync(int pawnLoadId, CancellationToken ct = default)
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
				if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
				var snap = new PawnPromptSnapshot
				{
					Id = new Identity
					{
						Name = pawn.Name?.ToStringShort ?? pawn.LabelCap ?? "Pawn",
						Gender = pawn.gender.ToString(),
						Age = pawn.ageTracker != null ? (int)UnityEngine.Mathf.Floor(pawn.ageTracker.AgeBiologicalYearsFloat) : 0,
						Race = pawn.def?.label ?? string.Empty,
						Belief = null
					},
					Story = new Backstory
					{
						Childhood = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetBackstoryTitle(pawn, true) ?? string.Empty,
						Adulthood = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetBackstoryTitle(pawn, false) ?? string.Empty
					},
					Traits = new TraitsAndWork
					{
						Traits = (pawn.story?.traits?.allTraits ?? new System.Collections.Generic.List<Trait>()).Select(t => t.LabelCap ?? t.Label).ToList(),
						WorkDisables = (RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetCombinedDisabledWorkTagsCsv(pawn) ?? string.Empty).Split(new[] { ',', ';' }, System.StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList()
					},
					Skills = new Skills
					{
						Items = (pawn.skills?.skills ?? new System.Collections.Generic.List<SkillRecord>()).Select(s => new SkillItem
						{
							Name = s.def?.label ?? s.def?.defName ?? string.Empty,
							Level = s.Level,
							Passion = s.passion.ToString(),
							Normalized = UnityEngine.Mathf.Clamp01(s.Level / 20f)
						}).ToList()
					},
					IsIdeologyAvailable = ModsConfig.IdeologyActive
				};
				if (snap.IsIdeologyAvailable)
				{
					try
					{
						// 尝试读取信仰/意识形态简要（若不可用则跳过）
						snap.Id.Belief = pawn.Ideo?.name ?? pawn.Ideo?.ToString() ?? null;
					}
					catch { snap.Id.Belief = null; }
				}
				return snap;
			}, name: "GetPawnPromptSnapshot", ct: cts.Token);
		}

		public Task<PawnSocialSnapshot> GetPawnSocialSnapshotAsync(int pawnLoadId, int topRelations, int recentSocialEvents, CancellationToken ct = default)
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
				if (pawn == null) throw new WorldDataException($"Pawn not found: {pawnLoadId}");
				var relations = new System.Collections.Generic.List<SocialRelationItem>();
				try
				{
					var rels = pawn.relations?.DirectRelations ?? new System.Collections.Generic.List<DirectPawnRelation>();
					foreach (var r in rels)
					{
						var other = r.otherPawn;
						if (other == null) continue;
						relations.Add(new SocialRelationItem
						{
							RelationKind = r.def?.label ?? r.def?.defName ?? string.Empty,
							OtherName = other.Name?.ToStringShort ?? other.LabelCap ?? "Pawn",
							OtherEntityId = $"pawn:{other.thingIDNumber}",
							Opinion = pawn.relations?.OpinionOf(other) ?? 0
						});
					}
				}
				catch { }
				var ordered = relations.OrderByDescending(x => x.Opinion).Take(Math.Max(0, topRelations)).ToList();
				var eventsList = new System.Collections.Generic.List<SocialEventItem>();
				try
				{
					var events = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetRecentSocialEvents(pawn, recentSocialEvents) ?? new System.Collections.Generic.List<SocialEventItem>();
					eventsList.AddRange(events);
				}
				catch { }
				return new PawnSocialSnapshot { Relations = ordered, RecentEvents = eventsList };
			}, name: "GetPawnSocialSnapshot", ct: cts.Token);
		}


	}

	internal sealed class WorldDataException : Exception
	{
		public WorldDataException(string message) : base(message) { }
		public WorldDataException(string message, Exception inner) : base(message, inner) { }
	}
}


