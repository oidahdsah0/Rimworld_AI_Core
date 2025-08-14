using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage.Triggers
{
	internal sealed class ProximityGroupChatTrigger : IStageTrigger
	{
		public string Name => "ProximityGroupChatTrigger";
		public string TargetActName => "GroupChat";

		public Task OnEnableAsync(CancellationToken ct) => Task.CompletedTask;
		public Task OnDisableAsync(CancellationToken ct) => Task.CompletedTask;

		public async Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct)
		{
			// 最小实现：构造一个假想的群聊参与者集合
			var intent = new StageIntent
			{
				ActName = TargetActName,
				ParticipantIds = new[] { "pawn:1", "pawn:2" },
				Origin = "Trigger",
				ScenarioText = "附近角色简短群聊",
				Locale = "zh-Hans"
			};
			await submit(intent);
		}
	}
}


