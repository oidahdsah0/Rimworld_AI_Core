using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	/// <summary>
	/// 定时触发器：每游戏小时（≈2500 ticks）有 1% 概率，随机挑一个小人作为中心，随机 2–5 人，1–3 轮，提交群聊意图。
	/// 轮数与人数上限后续进入设置面板；此处先用固定 2..5 与 1..3 逻辑。
	/// </summary>
	internal sealed class TimedGroupChatTrigger : IStageTrigger
	{
		private readonly IWorldDataService _world;
		private long _runCount = 0;

		public TimedGroupChatTrigger(IWorldDataService world) { _world = world; }

		public string Name => "TimedGroupChatTrigger";
		public string TargetActName => "GroupChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			try
			{
				// 触发器将被调度器每小时调用一次，这里仅做 1% 概率抽样。
				var seed = unchecked((int)DateTime.UtcNow.Ticks) ^ (int)System.Threading.Interlocked.Read(ref _runCount);
				var rnd = new Random(seed);
				System.Threading.Interlocked.Increment(ref _runCount);
				if (rnd.Next(0, 100) != 0) return;
				var ids = await _world.GetAllColonistLoadIdsAsync(ct).ConfigureAwait(false);
				var list = ids?.ToList() ?? new System.Collections.Generic.List<int>();
				if (list.Count < 2) return;
				int centerIdx = rnd.Next(0, list.Count);
				int center = list[centerIdx];
				// 确定人数与轮数（范围：2–5 人，1–3 轮）
				int count = Math.Max(2, Math.Min(5, 2 + rnd.Next(0, 4))); // 2..5
				int rounds = Math.Max(1, Math.Min(3, 1 + rnd.Next(0, 3))); // 1..3
				// 采样参与者，包含中心，去重
				var pool = list.Where(x => x != center).OrderBy(_ => rnd.Next()).Take(Math.Max(1, count - 1)).ToList();
				pool.Insert(0, center);
				var participants = pool.Select(x => $"pawn:{x}").ToList();
				var scenario = $"群聊触发：预期轮数={rounds}，参与者={string.Join(",", participants)}";
				var intent = new StageIntent
				{
					ActName = TargetActName,
					ParticipantIds = participants,
					Origin = "Timed",
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


