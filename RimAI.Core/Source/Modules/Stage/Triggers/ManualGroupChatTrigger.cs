using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	/// <summary>
	/// 主动触发器：调用时随机选一个中心小人并组队，立即提交群聊意图。可被 UI 或调试按钮调用。
	/// </summary>
	internal sealed class ManualGroupChatTrigger : IStageTrigger
	{
		private readonly IWorldDataService _world;
		private readonly Random _rnd = new Random();
		private volatile bool _armed;

		public ManualGroupChatTrigger(IWorldDataService world) { _world = world; }

		public string Name => "ManualGroupChatTrigger";
		public string TargetActName => "GroupChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		// 供 UI/按钮调用：置位一次性触发
		public void ArmOnce() { _armed = true; }

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			try
			{
				if (!_armed) return; // 未武装则不执行
				_armed = false; // 消耗一次
				var ids = await _world.GetAllColonistLoadIdsAsync(ct).ConfigureAwait(false);
				var list = ids?.ToList() ?? new System.Collections.Generic.List<int>();
				if (list.Count < 2) return;
				int centerIdx = _rnd.Next(0, list.Count);
				int center = list[centerIdx];
				int count = Math.Max(2, Math.Min(5, 2 + _rnd.Next(0, 4))); // 2..5
				int rounds = Math.Max(1, Math.Min(3, 1 + _rnd.Next(0, 3))); // 1..3
				var pool = list.Where(x => x != center).OrderBy(_ => _rnd.Next()).Take(Math.Max(1, count - 1)).ToList();
				pool.Insert(0, center);
				var participants = pool.Select(x => $"pawn:{x}").ToList();
				var scenario = $"群聊触发：预期轮数={rounds}，参与者={string.Join(",", participants)}";
				var intent = new StageIntent
				{
					ActName = TargetActName,
					ParticipantIds = participants,
					Origin = "Manual",
					ScenarioText = scenario,
					Locale = "zh-Hans",
					Seed = DateTime.UtcNow.Ticks.ToString()
				};
				await submit(intent).ConfigureAwait(false);
			}
			catch { }
		}
	}
}


