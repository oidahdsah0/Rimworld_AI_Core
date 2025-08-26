using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Scheduler;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Concurrent;
using Verse.AI;

namespace RimAI.Core.Source.Modules.World
{
	internal sealed class WorldActionService : IWorldActionService
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;
		private sealed class SessionState
		{
			public string Id;
			public int InitiatorLoadId;
			public System.Collections.Generic.List<int> ParticipantLoadIds;
			public int Radius;
			public System.TimeSpan MaxDuration;
			public System.DateTime StartedUtc;
			public bool Aborted;
			public bool Completed;
			public System.IDisposable Periodic;
		}

		private readonly ConcurrentDictionary<string, SessionState> _sessions = new ConcurrentDictionary<string, SessionState>();

		public WorldActionService(ISchedulerService scheduler, IConfigurationService cfg)
		{
			_scheduler = scheduler;
			_cfg = cfg as ConfigurationService ?? throw new InvalidOperationException("WorldActionService requires ConfigurationService");
		}

		public Task<bool> TryStartPartyAsync(int initiatorPawnLoadId, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					if (Current.Game == null) return false;
					Pawn initiator = null; Map map = null;
					foreach (var m in Find.Maps)
					{
						foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
						{
							if (p?.thingIDNumber == initiatorPawnLoadId) { initiator = p; map = m; break; }
						}
						if (initiator != null) break;
					}
					if (initiator == null || map == null) return false;
					var spot = initiator.Position;

					// 1) 优先尝试 GatheringsUtility.TryStartGathering(GatheringDef party, Pawn initiator, IntVec3 spot, bool forced)
					try
					{
						var asm = typeof(Pawn).Assembly;
						var tGatheringsUtility = asm.GetType("RimWorld.GatheringsUtility", throwOnError: false);
						var tGatheringDef = asm.GetType("RimWorld.GatheringDef", throwOnError: false);
						if (tGatheringsUtility != null && tGatheringDef != null)
						{
							// 从 DefDatabase<GatheringDef>.GetNamed("Party", false) 获取 party 定义
							var tDefDb = typeof(DefDatabase<>).MakeGenericType(tGatheringDef);
							object partyDef = null;
							var miGetNamed = tDefDb.GetMethod("GetNamed", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool) }, null);
							if (miGetNamed != null)
							{
								partyDef = miGetNamed.Invoke(null, new object[] { "Party", false });
							}
							else
							{
								var miGetNamedSilent = tDefDb.GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
								if (miGetNamedSilent != null) partyDef = miGetNamedSilent.Invoke(null, new object[] { "Party" });
							}
							if (partyDef != null)
							{
								// 反射 TryStartGathering
								var methods = tGatheringsUtility.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
								var tryStart = methods.FirstOrDefault(mi => mi.Name.Contains("TryStartGathering") && mi.GetParameters().Length >= 4);
								if (tryStart != null)
								{
									var param = tryStart.GetParameters();
									object[] args;
									if (param.Length == 4)
									{
										args = new object[] { partyDef, initiator, spot, true };
									}
									else
									{
										// 若有 Map 参数等其他形态，尽量填充（常见为 def, initiator, spot, forced）
										args = new object[param.Length];
										for (int i = 0; i < param.Length; i++)
										{
											var pt = param[i].ParameterType;
											if (pt == tGatheringDef) args[i] = partyDef;
											else if (pt == typeof(Pawn)) args[i] = initiator;
											else if (pt == typeof(IntVec3)) args[i] = spot;
											else if (pt == typeof(Map)) args[i] = map;
											else if (pt == typeof(bool)) args[i] = true;
											else args[i] = null;
										}
									}
									var ok = false;
									try { ok = (bool)(tryStart.Invoke(null, args) ?? false); } catch { ok = false; }
									if (ok) return true;
								}
							}
						}
					}
					catch { }

					// 2) 回退：LordMaker + LordJob_Joinable_Party
					try
					{
						var asm = typeof(Pawn).Assembly;
						var tLordMaker = asm.GetType("RimWorld.LordMaker", throwOnError: false);
						var tLordJobParty = asm.GetType("RimWorld.LordJob_Joinable_Party", throwOnError: false);
						var tGatheringDef = asm.GetType("RimWorld.GatheringDef", throwOnError: false);
						object partyDef = null;
						if (tGatheringDef != null)
						{
							var tDefDb = typeof(DefDatabase<>).MakeGenericType(tGatheringDef);
							var miGetNamed = tDefDb.GetMethod("GetNamed", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(bool) }, null);
							if (miGetNamed != null) partyDef = miGetNamed.Invoke(null, new object[] { "Party", false });
							else
							{
								var miGetNamedSilent = tDefDb.GetMethod("GetNamedSilentFail", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
								if (miGetNamedSilent != null) partyDef = miGetNamedSilent.Invoke(null, new object[] { "Party" });
							}
						}
						if (tLordMaker != null && tLordJobParty != null && partyDef != null)
						{
							// 找到最匹配的构造函数
							var ctors = tLordJobParty.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
							ConstructorInfo chosen = null;
							foreach (var c in ctors)
							{
								var ps = c.GetParameters().Select(p => p.ParameterType).ToArray();
								if (ps.Length >= 3 && ps[0] == typeof(IntVec3) && ps.Any(t => t == typeof(Pawn)) && ps.Any(t => t == tGatheringDef)) { chosen = c; break; }
							}
							if (chosen != null)
							{
								var ps = chosen.GetParameters();
								var ctorArgs = new object[ps.Length];
								for (int i = 0; i < ps.Length; i++)
								{
									var pt = ps[i].ParameterType;
									if (pt == typeof(IntVec3)) ctorArgs[i] = spot;
									else if (pt == typeof(Pawn)) ctorArgs[i] = initiator;
									else if (pt == tGatheringDef) ctorArgs[i] = partyDef;
									else if (pt == typeof(Map)) ctorArgs[i] = map;
									else if (pt == typeof(bool)) ctorArgs[i] = true;
									else ctorArgs[i] = null;
								}
								var job = chosen.Invoke(ctorArgs);
								var miMake = tLordMaker.GetMethod("MakeNewLord", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Faction), asm.GetType("RimWorld.LordJob"), typeof(Map), typeof(System.Collections.Generic.IEnumerable<Pawn>) }, null);
								if (miMake != null)
								{
									miMake.Invoke(null, new object[] { Faction.OfPlayer, job, map, null });
									return true;
								}
							}
						}
					}
					catch { }

					return false;
				}
				catch { return false; }
			}, name: "World.TryStartParty", ct: cts.Token);
		}

		public Task ShowSpeechTextAsync(int pawnLoadId, string text, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					Pawn pawn = null;
					foreach (var map in Find.Maps)
					{
						foreach (var p in map.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
						{
							if (p?.thingIDNumber == pawnLoadId) { pawn = p; break; }
						}
						if (pawn != null) break;
					}
					if (pawn == null) return Task.CompletedTask;
					var mapRef = pawn.Map; if (mapRef == null) return Task.CompletedTask;
					Color color = Color.white;
					try { color = Color.white; } catch { }
					try { MoteMaker.ThrowText(pawn.DrawPos, mapRef, text, color, 2f); } catch { }
					return Task.CompletedTask;
				}
				catch { return Task.CompletedTask; }
			}, name: "World.ShowSpeechText", ct: cts.Token);
		}

		public Task<GroupChatSessionHandle> StartGroupChatDutyAsync(int initiatorPawnLoadId, System.Collections.Generic.IReadOnlyList<int> participantLoadIds, int radius, System.TimeSpan maxDuration, CancellationToken ct = default)
		{
			if (participantLoadIds == null || participantLoadIds.Count == 0) return Task.FromResult<GroupChatSessionHandle>(null);
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			// 守护周期：每 120 tick（约 2 秒真实时间的 1/??，仅作巡检）
			const int guardEveryTicks = 120;
			var handle = new GroupChatSessionHandle { Id = System.Guid.NewGuid().ToString("N") };
			var state = new SessionState
			{
				Id = handle.Id,
				InitiatorLoadId = initiatorPawnLoadId,
				ParticipantLoadIds = participantLoadIds.Where(x => x != initiatorPawnLoadId).Distinct().ToList(),
				Radius = Mathf.Max(1, radius),
				MaxDuration = maxDuration,
				StartedUtc = System.DateTime.UtcNow,
				Aborted = false,
				Completed = false,
				Periodic = null
			};
			_sessions[state.Id] = state;

			// 将参与者移动至发起者周围并下发 Wait 任务
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			cts.CancelAfter(Math.Max(timeoutMs, 3000));
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					if (Current.Game == null) return (GroupChatSessionHandle)null;
					Pawn initiator = null; Map map = null;
					foreach (var m in Find.Maps)
					{
						foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
						{
							if (p?.thingIDNumber == initiatorPawnLoadId) { initiator = p; map = m; break; }
						}
						if (initiator != null) break;
					}
					if (initiator == null || map == null) return (GroupChatSessionHandle)null;

					foreach (var pid in state.ParticipantLoadIds)
					{
						try
						{
							Pawn pawn = null;
							foreach (var m in Find.Maps)
							{
								foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
								{
									if (p?.thingIDNumber == pid) { pawn = p; break; }
								}
								if (pawn != null) break;
							}
							if (pawn == null) continue;
							var dest = CellFinder.RandomClosewalkCellNear(initiator.Position, initiator.Map, state.Radius);
							try
							{
								try { if (pawn.drafter != null) pawn.drafter.Drafted = false; } catch { }
								pawn.jobs?.StartJob(new Job(JobDefOf.Goto, dest), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
								pawn.jobs?.jobQueue?.EnqueueLast(new Job(JobDefOf.Wait));
							}
							catch { }
						}
						catch { }
					}

					// 启动守护：中断条件/再下发 Wait
					state.Periodic = _scheduler.SchedulePeriodic("World.GroupChatGuard." + state.Id, guardEveryTicks, async token =>
					{
						bool abort = false;
						try
						{
							if ((System.DateTime.UtcNow - state.StartedUtc) > state.MaxDuration) abort = true;
							// 检查所有参与者
							var ids = new System.Collections.Generic.List<int>();
							ids.Add(state.InitiatorLoadId);
							ids.AddRange(state.ParticipantLoadIds);
							foreach (var id in ids)
							{
								Pawn pawn = null;
								foreach (var m in Find.Maps)
								{
									foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
									{
										if (p?.thingIDNumber == id) { pawn = p; break; }
									}
									if (pawn != null) break;
								}
								if (pawn == null) { abort = true; break; }
								// 中断条件：征召/倒地/极端精神状态/饥饿或极低休息
								bool drafted = false, downed = false, mental = false, hunger = false, rest = false;
								try { drafted = pawn.Drafted; } catch { }
								try { downed = pawn.Downed; } catch { }
								try { mental = pawn.mindState?.mentalStateHandler?.InMentalState ?? false; } catch { }
								try { var cat = pawn.needs?.food?.CurCategory; hunger = (cat == HungerCategory.UrgentlyHungry || cat == HungerCategory.Starving); } catch { }
								try { var rc = pawn.needs?.rest?.CurCategory; rest = (rc == RestCategory.VeryTired || rc == RestCategory.Exhausted); } catch { }
								if (drafted || downed || mental || hunger || rest) { abort = true; break; }
								// 额外：尝试通过反射检测“近期受击”字段（不同版本字段名可能不同）
								try
								{
									var ms = pawn.mindState;
									if (ms != null)
									{
										var tp = ms.GetType();
										var fields = tp.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
										int ticksNow = Find.TickManager.TicksGame;
										foreach (var f in fields)
										{
											if (f.FieldType == typeof(int))
											{
												var name = f.Name?.ToLowerInvariant() ?? string.Empty;
												if (name.Contains("damag") || name.Contains("harm"))
												{
													try
													{
														int v = (int)f.GetValue(ms);
														if (v > 0 && ticksNow - v <= 120) { abort = true; break; }
													}
													catch { }
												}
											}
										}
									}
								}
								catch { }
								if (abort) break;
							}

							// 维持 Wait：对非发起者保证处于 Wait 或 Goto→Wait 流程
							if (!abort)
							{
								foreach (var pid in state.ParticipantLoadIds)
								{
									Pawn pawn = null;
									foreach (var m in Find.Maps)
									{
										foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
										{
											if (p?.thingIDNumber == pid) { pawn = p; break; }
										}
										if (pawn != null) break;
									}
									if (pawn == null) continue;
									try
									{
										if (pawn.CurJobDef != JobDefOf.Wait && pawn.CurJobDef != JobDefOf.Goto)
										{
											var dest = CellFinder.RandomClosewalkCellNear(initiator.Position, initiator.Map, state.Radius);
											pawn.jobs?.StartJob(new Job(JobDefOf.Goto, dest), JobCondition.InterruptForced, null, resumeCurJobAfterwards: false);
											pawn.jobs?.jobQueue?.EnqueueLast(new Job(JobDefOf.Wait));
										}
									}
									catch { }
								}
							}
						}
						catch { abort = true; }
						if (abort)
						{
							try { await EndGroupChatDutyAsync(handle, "Aborted", CancellationToken.None).ConfigureAwait(false); } catch { }
						}
					}, CancellationToken.None);

					return handle;
				}
				catch { return (GroupChatSessionHandle)null; }
			}, name: "World.StartGroupChatDuty", ct: cts.Token);
		}

		public Task<bool> EndGroupChatDutyAsync(GroupChatSessionHandle handle, string reason, CancellationToken ct = default)
		{
			if (handle == null || string.IsNullOrWhiteSpace(handle.Id)) return Task.FromResult(false);
			if (!_sessions.TryRemove(handle.Id, out var state)) return Task.FromResult(false);
			state.Aborted = string.Equals(reason, "Aborted", System.StringComparison.OrdinalIgnoreCase);
			state.Completed = !state.Aborted;
			try { state.Periodic?.Dispose(); } catch { }
			// 清理参与者 Job
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(Math.Max(timeoutMs, 2000));
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					var ids = new System.Collections.Generic.List<int>();
					ids.Add(state.InitiatorLoadId);
					ids.AddRange(state.ParticipantLoadIds);
					foreach (var id in ids)
					{
						try
						{
							Pawn pawn = null;
							foreach (var m in Find.Maps)
							{
								foreach (var p in m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
								{
									if (p?.thingIDNumber == id) { pawn = p; break; }
								}
								if (pawn != null) break;
							}
							if (pawn == null) continue;
							try { pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced, true); } catch { }
						}
						catch { }
					}
					return true;
				}
				catch { return true; }
			}, name: "World.EndGroupChatDuty", ct: cts.Token);
		}

		public bool IsGroupChatSessionAlive(GroupChatSessionHandle handle)
		{
			if (handle == null || string.IsNullOrWhiteSpace(handle.Id)) return false;
			if (!_sessions.TryGetValue(handle.Id, out var state)) return false;
			if (state == null) return false;
			if (state.Aborted) return false;
			if ((System.DateTime.UtcNow - state.StartedUtc) > state.MaxDuration) return false;
			return true;
		}

		public Task ShowTopLeftMessageAsync(string text, Verse.MessageTypeDef type, CancellationToken ct = default)
		{
			if (string.IsNullOrWhiteSpace(text)) return Task.CompletedTask;
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(timeoutMs);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try { Messages.Message(text, type ?? MessageTypeDefOf.NeutralEvent); } catch { }
				return Task.CompletedTask;
			}, name: "World.ShowTopLeftMessage", ct: cts.Token);
		}

		public Task DropUnknownCivGiftAsync(float quantityCoefficient = 1.0f, CancellationToken ct = default)
		{
			var timeoutMs = _cfg.GetWorldDataConfig().DefaultTimeoutMs;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			cts.CancelAfter(Math.Max(timeoutMs, 3000));
			quantityCoefficient = Mathf.Max(0.1f, quantityCoefficient);
			return _scheduler.ScheduleOnMainThreadAsync(() =>
			{
				try
				{
					var map = Find.Maps?.FirstOrDefault(m => m.IsPlayerHome) ?? Find.CurrentMap ?? Find.Maps?.FirstOrDefault();
					if (map == null) return Task.CompletedTask;
					IntVec3 spot = DropCellFinder.TradeDropSpot(map);
					var things = RimWorld.ThingSetMakerDefOf.ResourcePod.root.Generate();
					foreach (var t in things)
					{
						try { t.stackCount = Mathf.Max(1, Mathf.CeilToInt(t.stackCount * quantityCoefficient)); } catch { }
					}
					try { RimWorld.DropPodUtility.DropThingsNear(spot, map, things, 110, canInstaDropDuringInit: false, leaveSlag: false, canRoofPunch: true, forbid: true); } catch { }
				}
				catch { }
				return Task.CompletedTask;
			}, name: "World.DropUnknownCivGift", ct: cts.Token);
		}
	}
}


