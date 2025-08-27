using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Tooling;

namespace RimAI.Core.Source.Modules.Orchestration.Modes
{
	internal sealed class ClassicMode : IToolMatchMode
	{
		private readonly IToolRegistryService _tooling;

		public ClassicMode(IToolRegistryService tooling)
		{
			_tooling = tooling;
		}

		public Task<(IReadOnlyList<string> toolsJson, IReadOnlyList<(string name, double score)> scores, string error)> GetToolsAsync(
			string userInput,
			IReadOnlyList<string> participantIds,
			OrchestrationMode mode,
			ToolOrchestrationOptions options,
			CancellationToken ct)
		{
			// 统一入口：由 ToolRegistryService.BuildToolsAsync 负责等级/研究过滤
			var t = _tooling.BuildToolsAsync(RimAI.Core.Contracts.Config.ToolCallMode.Classic, userInput, null, null, new ToolQueryOptions(), ct);
			return t;
		}

		private static string ExtractName(string toolJson)
		{
			if (string.IsNullOrEmpty(toolJson)) return null;
			// 极简提取：寻找 \"Name\": "..."
			var key = "\"Name\":";
			var idx = toolJson.IndexOf(key);
			if (idx < 0) return null;
			idx += key.Length;
			while (idx < toolJson.Length && (toolJson[idx] == ' ' || toolJson[idx] == '\t' || toolJson[idx] == '"' || toolJson[idx] == '\'' || toolJson[idx] == ':')) idx++;
			var end = idx;
			while (end < toolJson.Length && toolJson[end] != '"' && toolJson[end] != '\'' && toolJson[end] != ',' && toolJson[end] != '}') end++;
			return toolJson.Substring(idx, end - idx).Trim('"','\'',' ');
		}
	}
}


