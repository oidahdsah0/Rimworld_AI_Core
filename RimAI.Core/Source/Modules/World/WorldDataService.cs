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
				// 采集 Hediffs（受伤/疾病/改造/缺失等）
				try
				{
					var list = new System.Collections.Generic.List<HediffItem>();
					var hediffs = pawn.health?.hediffSet?.hediffs ?? new System.Collections.Generic.List<Hediff>();
					foreach (var hdf in hediffs)
					{
						if (hdf == null) continue;
						var label = hdf.LabelBaseCap ?? hdf.LabelCap ?? hdf.def?.label ?? hdf.def?.defName ?? string.Empty;
						var part = hdf.Part?.LabelCap ?? string.Empty;
						var sev = 0f; try { sev = hdf.Severity; } catch { sev = 0f; }
						bool perm = false; try { perm = hdf.IsPermanent(); } catch { perm = false; }
						string cat = "Other";
						try
						{
							if (hdf is Hediff_MissingPart) cat = "MissingPart";
							else if (hdf is Hediff_AddedPart) cat = "Implant";
							else if (hdf.def?.injuryProps != null) cat = "Injury";
							else if (hdf.def?.isBad == true) cat = "Disease";
						}
						catch { }
						list.Add(new HediffItem { Label = label, Part = part, Severity = sev, Permanent = perm, Category = cat });
					}
					snap.Hediffs = list;
				}
				catch { }
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

		public Task<EnvironmentMatrixSnapshot> GetPawnEnvironmentMatrixAsync(int pawnLoadId, int radius, CancellationToken ct = default)
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
				var mapRef = pawn.Map;
				if (mapRef == null) throw new WorldDataException("Pawn map missing");
				var center = pawn.Position;
				int r = Math.Max(1, radius);
				int size = r * 2 + 1;
				var rows = new System.Collections.Generic.List<string>(size);
				float beautySum = 0f; long beautyN = 0;
				var terrainCounts = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
				for (int dy = r; dy >= -r; dy--)
				{
					var sb = new System.Text.StringBuilder(size);
					for (int dx = -r; dx <= r; dx++)
					{
						var cell = center + new IntVec3(dx, 0, dy);
						char ch = '?';
						if (!cell.InBounds(mapRef))
						{
							ch = '#'; // 越界
						}
						else if (cell == center)
						{
							ch = 'P'; // Pawn 本人
						}
						else
						{
							var thing = cell.GetFirstPawn(mapRef);
							if (thing != null) ch = 'p';
							else if (cell.GetFirstItem(mapRef) != null) ch = 'i';
							else if (cell.GetEdifice(mapRef) != null) ch = 'B';
							else if (cell.GetTerrain(mapRef)?.affordances?.Count > 0) ch = '.'; // 地形可站立
							else ch = '~'; // 其他/液体/不可站立
							try
							{
								float b = BeautyUtility.CellBeauty(cell, mapRef);
								beautySum += b; beautyN++;
								var terr = cell.GetTerrain(mapRef);
								var key = terr?.label ?? terr?.defName ?? "(unknown)";
								if (!terrainCounts.TryGetValue(key, out var cnt)) cnt = 0;
								terrainCounts[key] = cnt + 1;
							}
							catch { }
						}
						sb.Append(ch);
					}
					rows.Add(sb.ToString());
				}
				var legend = "P=自我 p=其他小人 i=物品 B=建筑 .=地形可站立 ~=不可站立 #=越界";
				var terrainList = new System.Collections.Generic.List<TerrainCountItem>(terrainCounts.Count);
				foreach (var kv in terrainCounts) terrainList.Add(new TerrainCountItem { Terrain = kv.Key, Count = kv.Value });
				terrainList.Sort((a, b) => b.Count.CompareTo(a.Count));
				return new EnvironmentMatrixSnapshot { PawnLoadId = pawnLoadId, Radius = r, Rows = rows, Legend = legend, BeautyAverage = (beautyN > 0 ? (beautySum / beautyN) : 0f), TerrainCounts = terrainList };
			}, name: "GetPawnEnvironmentMatrix", ct: cts.Token);
		}

		public Task<float> GetBeautyAverageAsync(int centerX, int centerZ, int radius, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap;
				if (map == null) throw new WorldDataException("Map missing");
				int r = Math.Max(0, radius);
				long n = 0;
				double sum = 0.0;
				for (int dz = -r; dz <= r; dz++)
				{
					for (int dx = -r; dx <= r; dx++)
					{
						var cell = new IntVec3(centerX + dx, 0, centerZ + dz);
						if (!cell.InBounds(map)) continue;
						float beauty = 0f;
						try { beauty = BeautyUtility.CellBeauty(cell, map); } catch { beauty = 0f; }
						sum += beauty;
						n++;
					}
				}
				return (float)(n > 0 ? (sum / n) : 0.0);
			}, name: "GetBeautyAverage", ct: cts.Token);
		}

		public Task<System.Collections.Generic.IReadOnlyList<TerrainCountItem>> GetTerrainCountsAsync(int centerX, int centerZ, int radius, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var map = Find.CurrentMap;
				if (map == null) throw new WorldDataException("Map missing");
				int r = Math.Max(0, radius);
				var counting = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
				for (int dz = -r; dz <= r; dz++)
				{
					for (int dx = -r; dx <= r; dx++)
					{
						var cell = new IntVec3(centerX + dx, 0, centerZ + dz);
						if (!cell.InBounds(map)) continue;
						var terr = cell.GetTerrain(map);
						var key = terr?.label ?? terr?.defName ?? "(unknown)";
						if (!counting.TryGetValue(key, out var c)) c = 0;
						counting[key] = c + 1;
					}
				}
				var list = new System.Collections.Generic.List<TerrainCountItem>(counting.Count);
				foreach (var kv in counting)
				{
					list.Add(new TerrainCountItem { Terrain = kv.Key, Count = kv.Value });
				}
				return (System.Collections.Generic.IReadOnlyList<TerrainCountItem>)list;
			}, name: "GetTerrainCounts", ct: cts.Token);
		}

		public Task<ColonySnapshot> GetColonySnapshotAsync(int? pawnLoadId, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				if (Current.Game == null) throw new WorldDataException("World not loaded");
				var faction = Faction.OfPlayer;
				var map = Find.CurrentMap;
				string colonyName = faction?.Name ?? map?.Parent?.Label ?? "Colony";
				var names = new System.Collections.Generic.List<string>();
				var records = new System.Collections.Generic.List<ColonistRecord>();
				int count = 0;
				foreach (var m in Find.Maps)
				{
					var pawns = m?.mapPawns?.PawnsInFaction(faction);
					if (pawns == null) continue;
					foreach (var p in pawns)
					{
						if (p == null || p.RaceProps == null || p.RaceProps.Humanlike == false) continue;
						if (p.HostFaction != null) continue; // exclude prisoners/guests of other factions
						var dispName = p.Name?.ToStringShort ?? p.LabelCap ?? "Pawn";
						names.Add(dispName);
						var age = p.ageTracker != null ? (int)UnityEngine.Mathf.Floor(p.ageTracker.AgeBiologicalYearsFloat) : 0;
						var gender = p.gender.ToString();
						// 职业/称号：优先 Royalty/Story Title，再回退 Backstory
						string job = null;
						try { job = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetPawnTitle(p); } catch { }
						records.Add(new ColonistRecord { Name = dispName, Age = age, Gender = gender, JobTitle = job ?? string.Empty });
						count++;
					}
				}
				return new ColonySnapshot { ColonyName = colonyName, ColonistCount = count, ColonistNames = names, Colonists = records };
			}, name: "GetColonySnapshot", ct: cts.Token);
		}

	}

	internal sealed class WorldDataException : Exception
	{
		public WorldDataException(string message) : base(message) { }
		public WorldDataException(string message, Exception inner) : base(message, inner) { }
	}
}


