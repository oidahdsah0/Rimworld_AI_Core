using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Stage.Models;

namespace RimAI.Core.Source.Modules.Stage
{
	internal interface IStageTrigger
	{
		string Name { get; }
		string TargetActName { get; }
		Task OnEnableAsync(CancellationToken ct);
		Task OnDisableAsync(CancellationToken ct);
		Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct);
	}
}


