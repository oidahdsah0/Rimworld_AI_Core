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
			var res = _tooling.GetClassicToolCallSchema(new ToolQueryOptions());
			var names = new List<(string, double)>();
			// 仅用于日志：从 JSON 粗略抓取 name 字段，避免引用 Framework 类型
			var topNames = res.ToolsJson?.Take(5).Select(j => ExtractName(j)).Where(n => !string.IsNullOrEmpty(n)).Select(n => (n, 1.0)).ToList() ?? new List<(string,double)>();
			return Task.FromResult(((IReadOnlyList<string>)res.ToolsJson, (IReadOnlyList<(string,double)>)topNames, (string)null));
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


