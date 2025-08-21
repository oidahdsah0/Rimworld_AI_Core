using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.Stage.Diagnostics;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	/// <summary>
	/// 全局唯一触发器：
	/// - 每游戏小时被调度一次，内部以 1% 概率触发；
	/// - 命中后在支持自动意图的 Acts 中“均等随机”选择一个，尝试构造并提交 Intent；
	/// - 也支持手动触发（ArmOnce），下一次调度必定尝试一次（忽略 1% 概率）。
	/// </summary>
	internal sealed class GlobalTimedRandomActTrigger : IStageTrigger, IManualStageTrigger
	{
		private readonly IStageService _stage;
		private readonly IStageLogging _log;
		private readonly Random _rnd;
		private volatile bool _armed;
		private volatile bool _enabled;

		public GlobalTimedRandomActTrigger(IStageService stage, IStageLogging log)
		{
			_stage = stage;
			_log = log;
			_rnd = new Random(unchecked(Environment.TickCount ^ 0x7f31e2a1));
		}

		public string Name => "GlobalTimedRandomActTrigger";
		public string TargetActName => string.Empty; // 动态选择，无固定目标

		public Task OnEnableAsync(CancellationToken ct) { _enabled = true; return Task.CompletedTask; }
		public Task OnDisableAsync(CancellationToken ct) { _enabled = false; return Task.CompletedTask; }

		public void ArmOnce() { _armed = true; try { _log?.Info("GlobalTrigger armed once"); } catch { } }

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			if (!_enabled) return;
			bool force = _armed; _armed = false;
			if (!force)
			{
				// 1% 概率
				if (_rnd.Next(0, 100) != 0) return;
			}

			try { _log?.Info($"TriggerHit mode={(force ? "manual" : "random")}"); } catch { }

			List<string> actNames;
			try { actNames = _stage.ListActs()?.ToList() ?? new List<string>(); }
			catch { actNames = new List<string>(); }
			if (actNames.Count == 0) return;

			// 均等随机顺序尝试：仅对实现了 IAutoStageIntentProvider 的 Act
			var shuffled = actNames.OrderBy(_ => _rnd.Next()).ToList();
			foreach (var name in shuffled)
			{
				if (ct.IsCancellationRequested) return;
				var auto = _stage.TryGetAutoProvider(name);
				if (auto == null) continue;
				try
				{
					var intent = await auto.TryBuildAutoIntentAsync(ct).ConfigureAwait(false);
					if (intent == null) continue;
					try { _log?.Info($"ActPicked name={intent.ActName} participants={(intent.ParticipantIds?.Count ?? 0)} origin={intent.Origin}"); } catch { }
					await submit(intent).ConfigureAwait(false);
					return;
				}
				catch { }
			}

			try { _log?.Info("ActPick none-available"); } catch { }
		}
	}
}



