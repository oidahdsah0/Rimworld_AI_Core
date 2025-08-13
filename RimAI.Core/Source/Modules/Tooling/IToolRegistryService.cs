using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Tooling
{
	// 与文档一致：内部 API，不暴露到 Contracts
internal sealed class ToolQueryOptions
	{
	public IReadOnlyList<string> IncludeWhitelist { get; set; }
	public IReadOnlyList<string> ExcludeBlacklist { get; set; }
	}

internal sealed class ToolClassicResult
{
	// 返回可直接发送给 Framework 的 Tool JSON 列表（字符串形式以避免直接引用 Framework 类型）
	public IReadOnlyList<string> ToolsJson { get; set; }
}

internal sealed class ToolNarrowTopKResult
{
	public IReadOnlyList<string> ToolsJson { get; set; }
	public IReadOnlyList<Indexing.ToolScore> Scores { get; set; }
}

internal interface IToolRegistryService
{
	ToolClassicResult GetClassicToolCallSchema(ToolQueryOptions options = null);

	Task<ToolNarrowTopKResult> GetNarrowTopKToolCallSchemaAsync(
		string userInput,
		int k,
		double? minScore,
		ToolQueryOptions options = null,
		CancellationToken ct = default);

	// 执行演示工具（S10 最小实现，参数校验在空参数下天然通过）
	Task<object> ExecuteToolAsync(string toolName, System.Collections.Generic.Dictionary<string, object> args, CancellationToken ct = default);

	// 索引生命周期与状态
	bool IsIndexReady();
	Indexing.ToolIndexFingerprint GetIndexFingerprint();
	Task EnsureIndexBuiltAsync(CancellationToken ct = default);
	Task RebuildIndexAsync(CancellationToken ct = default);
	void MarkIndexStale();
}
}


