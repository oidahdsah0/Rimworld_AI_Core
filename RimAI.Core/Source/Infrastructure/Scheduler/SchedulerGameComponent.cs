using System;
using RimAI.Core.Source.Boot;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using Verse;
using RimWorld;

namespace RimAI.Core.Source.Infrastructure.Scheduler
{
	/// <summary>
	/// 主线程泵：每 Tick 在主线程执行队列中的工作项，遵循预算与限额。
	/// </summary>
	public sealed class SchedulerGameComponent : GameComponent
	{
		private SchedulerService _scheduler; // concrete for internal calls
		private ConfigurationService _cfg;
		private bool _bound;
		private bool _printedReady;
		private bool _toolingEnsured;
		private bool _toolingVerified; // 新增：索引校验标记

		public SchedulerGameComponent(Game game) { }

		public override void GameComponentTick()
		{
			EnsureBound();
			var tick = Find.TickManager.TicksGame;
			_scheduler.ProcessFrame(tick,
				msg => Log.Message(msg),
				warn => Log.Warning(warn),
				err => Log.Error(err));

			// 每小时间隔检查一次触发器（抽样在触发器内部处理）
			try
			{
				if (tick % 2500 == 0)
				{
					var stage = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Stage.IStageService>();
					_ = System.Threading.Tasks.Task.Run(async () => { try { await stage.RunActiveTriggersOnceAsync(System.Threading.CancellationToken.None); } catch { } });
				}
			}
			catch { }

			// P4: 在第 1000 Tick 后确保索引构建（后台非阻塞）
			if (!_toolingEnsured && tick >= 1000)
			{
				_toolingEnsured = true;
				try
				{
					var tooling = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>();
					_ = System.Threading.Tasks.Task.Run(async () =>
					{
						try { await tooling.EnsureIndexBuiltAsync(); }
						catch (System.Exception ex) { Log.Error($"[RimAI.Core][P4] EnsureIndexBuiltAsync failed: {ex.Message}"); }
					});
				}
				catch { }
			}

			// 新增：在小人落地后（第 2000 Tick）异步校验索引文件与工具列表是否匹配；若无文件或不匹配则自动重建
			if (!_toolingVerified && tick >= 2000)
			{
				KickoffToolIndexVerifyAsync();
			}

			// P13：在第 1000 Tick 后尝试发现服务器并注册周期任务（幂等）
			if (tick == 1000)
			{
				try
				{
					var world = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.World.IWorldDataService>();
					var server = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
					_ = System.Threading.Tasks.Task.Run(async () =>
					{
						try
						{
							var ids = await world.GetPoweredAiServerThingIdsAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
							foreach (var id in ids)
							{
								int level = 1; try { level = await world.GetAiServerLevelAsync(id).ConfigureAwait(false); } catch { level = 1; }
								server.GetOrCreate($"thing:{id}", level);
							}
							server.StartAllSchedulers(System.Threading.CancellationToken.None);
						}
						catch { }
					});
				}
				catch { }
			}

			if (!_printedReady)
			{
				_printedReady = true;
				Log.Message("[RimAI.Core][P3] Scheduler ready");
			}
		}

		private void EnsureBound()
		{
			if (_bound) return;
			try
			{
				var container = RimAICoreMod.Container;
				_cfg = container.Resolve<IConfigurationService>() as ConfigurationService;
				_scheduler = container.Resolve<ISchedulerService>() as SchedulerService;
				if (_cfg == null || _scheduler == null) return;
				_scheduler.BindMainThread(Environment.CurrentManagedThreadId);
				_bound = true;
			}
			catch (Exception ex)
			{
				Log.Error($"[RimAI.Core][P3] SchedulerGameComponent binding failed: {ex}");
			}
		}

		public override void LoadedGame()
		{
			// 读档完成后再校验一次（与 Tick 触发逻辑合并使用同一方法，具备幂等）
			KickoffToolIndexVerifyAsync();
		}

		public override void StartedNewGame()
		{
			// 新开局也触发一次校验（与 Tick 触发逻辑合并使用同一方法，具备幂等）
			KickoffToolIndexVerifyAsync();
		}

		private void KickoffToolIndexVerifyAsync()
		{
			if (_toolingVerified) return;
			_toolingVerified = true;
			try
			{
				var tooling = RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>();
				_ = System.Threading.Tasks.Task.Run(async () =>
				{
					try
					{
						bool ok = await tooling.CheckIndexMatchesToolsAsync();
						if (!ok)
						{
							try { Messages.Message("[RimAI.Core] Tool index missing or out-of-date. Rebuilding...", MessageTypeDefOf.NeutralEvent, false); } catch { }
							Log.Message("[RimAI.Core][P4] Tool index missing/mismatch, rebuilding...");
							await tooling.RebuildIndexAsync();
							try { Messages.Message("[RimAI.Core] Tool index rebuild completed.", MessageTypeDefOf.PositiveEvent, false); } catch { }
							Log.Message("[RimAI.Core][P4] Tool index rebuild completed.");
						}
					}
					catch (System.Exception ex)
					{
						try { Messages.Message("[RimAI.Core] Tool index verify/rebuild failed.", MessageTypeDefOf.NegativeEvent, false); } catch { }
						Log.Error($"[RimAI.Core][P4] Tool index verify/rebuild failed: {ex.Message}");
					}
				});
			}
			catch { }
		}

		internal SchedulerSnapshot GetSnapshot() => _scheduler?.GetSnapshot();
	}
}


