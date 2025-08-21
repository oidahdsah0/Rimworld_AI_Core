using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RimAI.Core.Source.Modules.Stage.Models;
using RimAI.Core.Source.Modules.World;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	internal sealed class TimedInterServerChatTrigger : IStageTrigger
	{
		private readonly IWorldDataService _world;
		private readonly Random _rnd = new Random(unchecked(Environment.TickCount ^ 0x6113cafe));

		public TimedInterServerChatTrigger(IWorldDataService world) { _world = world; }

		public string Name => "TimedInterServerChatTrigger";
		public string TargetActName => "InterServerGroupChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			try
			{
				var poweredIds = await _world.GetPoweredAiServerThingIdsAsync(ct).ConfigureAwait(false);
				var count = poweredIds?.Count ?? 0;
				if (count <= 2) return;
				// 1% 概率触发
				if (_rnd.Next(0, 100) != 0) return;

				int kMax = Math.Min(5, count);
				int k = 2 + _rnd.Next(0, Math.Max(1, kMax - 2 + 1)); // [2, kMax]
				var pick = poweredIds.ToList();
				// 洗牌并取前 k
				for (int i = pick.Count - 1; i > 0; i--)
				{
					int j = _rnd.Next(0, i + 1);
					var tmp = pick[i];
					pick[i] = pick[j];
					pick[j] = tmp;
				}
				var chosen = pick.Take(k).Select(id => $"thing:{id}").ToList();
				var scenario = $"servers={string.Join(",", chosen)}";
				var intent = new StageIntent
				{
					ActName = TargetActName,
					ParticipantIds = new[] { "agent:server_hub", "player:servers" },
					Origin = "TimedInterServer",
					ScenarioText = scenario,
					Locale = "zh-Hans"
				};
				await submit(intent);
			}
			catch { }
		}
	}
}


