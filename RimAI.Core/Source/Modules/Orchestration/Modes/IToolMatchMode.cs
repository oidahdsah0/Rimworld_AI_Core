using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Orchestration.Modes
{
	internal interface IToolMatchMode
	{
		Task<(IReadOnlyList<string> toolsJson, IReadOnlyList<(string name, double score)> scores, string error)> GetToolsAsync(
			string userInput,
			IReadOnlyList<string> participantIds,
			OrchestrationMode mode,
			ToolOrchestrationOptions options,
			CancellationToken ct);
	}
}


