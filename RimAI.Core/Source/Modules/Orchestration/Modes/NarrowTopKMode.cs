using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Tooling;

namespace RimAI.Core.Source.Modules.Orchestration.Modes
{
	internal sealed class NarrowTopKMode : IToolMatchMode
	{
		private readonly IToolRegistryService _tooling;

		public NarrowTopKMode(IToolRegistryService tooling)
		{
			_tooling = tooling;
		}

		public async Task<(IReadOnlyList<string> toolsJson, IReadOnlyList<(string name, double score)> scores, string error)> GetToolsAsync(
			string userInput,
			IReadOnlyList<string> participantIds,
			OrchestrationMode mode,
			ToolOrchestrationOptions options,
			CancellationToken ct)
		{
			try
			{
				var res = await _tooling.GetNarrowTopKToolCallSchemaAsync(userInput, options?.NarrowTopK ?? 5, options?.MinScoreThreshold, null, ct).ConfigureAwait(false);
				var pairs = res.Scores?.Select(s => (s.ToolName ?? string.Empty, s.Score)).ToList() ?? new List<(string,double)>();
				return (res.ToolsJson ?? Array.Empty<string>(), pairs, null);
			}
			catch (ToolIndexNotReadyException ex)
			{
				return (Array.Empty<string>(), Array.Empty<(string,double)>(), ex.Message ?? "index_not_ready");
			}
		}
	}
}


