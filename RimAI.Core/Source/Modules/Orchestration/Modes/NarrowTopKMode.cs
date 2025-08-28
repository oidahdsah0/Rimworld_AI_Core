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
			// 统一入口：由 ToolRegistryService.BuildToolsAsync 负责等级/研究过滤 + TopK 召回；最大等级与白名单从 options 透传
			var res = await _tooling.BuildToolsAsync(
				RimAI.Core.Contracts.Config.ToolCallMode.TopK,
				userInput,
				options?.NarrowTopK,
				options?.MinScoreThreshold,
				new ToolQueryOptions { MaxToolLevel = options?.MaxToolLevel ?? 3, IncludeWhitelist = options?.IncludeWhitelist },
				ct).ConfigureAwait(false);
			return res;
		}
	}
}


