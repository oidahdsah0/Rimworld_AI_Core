using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Orchestration
{
internal enum OrchestrationMode { Classic, NarrowTopK }

internal enum ExecutionProfile { Fast, Deep }

internal sealed class ToolOrchestrationOptions
{
	public OrchestrationMode Mode { get; set; } = OrchestrationMode.Classic;
	public ExecutionProfile Profile { get; set; } = ExecutionProfile.Fast;
	public int MaxCalls { get; set; } = 1;
	public int NarrowTopK { get; set; } = 5;
	public double? MinScoreThreshold { get; set; }
	public string Locale { get; set; } = "zh-Hans";
	// 由编排层注入的“最大工具等级”限制（例如：若存在 pawn: 则强制为 1）。
	public int? MaxToolLevel { get; set; }
}

internal sealed class ToolCallRecord
{
	public string CallId { get; set; }
	public string ToolName { get; set; }
	public Dictionary<string, object> Args { get; set; }
	public string GroupId { get; set; }
	public int Order { get; set; }
	public IReadOnlyList<string> DependsOn { get; set; }
}

internal sealed class ToolExecutionRecord
{
	public string CallId { get; set; }
	public string GroupId { get; set; }
	public string ToolName { get; set; }
	public Dictionary<string, object> Args { get; set; }
	public string Outcome { get; set; } // success | validation_error | unavailable | rate_limited | timeout | exception | invalid_name
	public object ResultObject { get; set; }
	public int LatencyMs { get; set; }
	public int Attempt { get; set; }
	public DateTime StartedAtUtc { get; set; }
	public DateTime FinishedAtUtc { get; set; }
}

internal sealed class ToolCallsResult
{
	public OrchestrationMode Mode { get; set; }
	public ExecutionProfile Profile { get; set; }
	public IReadOnlyList<string> ExposedTools { get; set; }
	public IReadOnlyList<ToolCallRecord> DecidedCalls { get; set; }
	public IReadOnlyList<ToolExecutionRecord> Executions { get; set; }
	public bool IsSuccess { get; set; }
	public string Error { get; set; }
	public int TotalLatencyMs { get; set; }
	public IReadOnlyList<string> PlanTrace { get; set; }
	public string HitDisplayName { get; set; }
}

	internal interface IOrchestrationService
	{
		Task<ToolCallsResult> ExecuteAsync(
			string userInput,
			IReadOnlyList<string> participantIds,
			ToolOrchestrationOptions options,
			CancellationToken ct = default);
	}
}


