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

namespace RimAI.Core.Source.Modules.World
{
	internal sealed class WorldActionService : IWorldActionService
	{
		private readonly ISchedulerService _scheduler;
		private readonly ConfigurationService _cfg;

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
	}
}


