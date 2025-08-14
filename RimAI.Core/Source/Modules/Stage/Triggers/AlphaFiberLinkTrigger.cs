using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	internal sealed class AlphaFiberLinkTrigger : IStageTrigger
	{
		public string Name => "AlphaFiberLinkTrigger";
		public string TargetActName => "AlphaFiberInterServerChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			// 最小实现：构造一对假想的服务器 pair
			var intent = new StageIntent
			{
				ActName = TargetActName,
				ParticipantIds = new[] { "thing:serverA", "thing:serverB" },
				Origin = "AlphaFiber",
				ScenarioText = "Alpha 光纤服务器链路对话",
				Locale = "zh-Hans"
			};
			await submit(intent);
		}
	}
}


