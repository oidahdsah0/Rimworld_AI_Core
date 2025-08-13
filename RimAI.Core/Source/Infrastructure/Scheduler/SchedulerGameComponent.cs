using System;
using RimAI.Core.Source.Boot;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Infrastructure.Configuration;
using Verse;

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

		public SchedulerGameComponent(Game game) { }

		public override void GameComponentTick()
		{
			EnsureBound();
			var tick = Find.TickManager.TicksGame;
			_scheduler.ProcessFrame(tick,
				msg => Log.Message(msg),
				warn => Log.Warning(warn),
				err => Log.Error(err));

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

		internal SchedulerSnapshot GetSnapshot() => _scheduler?.GetSnapshot();
	}
}


