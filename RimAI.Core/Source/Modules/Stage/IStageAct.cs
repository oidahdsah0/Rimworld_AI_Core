using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage
{
	internal interface IStageAct
	{
		string Name { get; }
		bool IsEligible(StageExecutionRequest req);
		Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct);
		Task OnEnableAsync(CancellationToken ct);
		Task OnDisableAsync(CancellationToken ct);
	}
}


