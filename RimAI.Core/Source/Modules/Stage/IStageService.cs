using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage
{
	internal interface IStageService
	{
		void RegisterAct(IStageAct act);
		void UnregisterAct(string name);
		void EnableAct(string name);
		void DisableAct(string name);
		IReadOnlyList<string> ListActs();

		void RegisterTrigger(IStageTrigger trigger);
		void UnregisterTrigger(string name);
		void EnableTrigger(string name);
		void DisableTrigger(string name);
		IReadOnlyList<string> ListTriggers();

		// 触发器扩展：为支持主动触发（例如手动按钮），提供一次性武装接口
		bool ArmTrigger(string name);

		Task<StageDecision> SubmitIntentAsync(StageIntent intent, CancellationToken ct);
		Task<ActResult> StartAsync(string actName, StageExecutionRequest req, CancellationToken ct);
		IReadOnlyList<RunningActInfo> QueryRunning();
		Task RunActiveTriggersOnceAsync(CancellationToken ct);
		void ForceRelease(string ticketId);
		void ClearIdempotencyCache();
	}
}


