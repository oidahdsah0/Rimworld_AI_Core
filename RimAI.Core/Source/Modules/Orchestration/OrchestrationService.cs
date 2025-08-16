using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Orchestration.Modes;
using RimAI.Core.Source.Modules.Tooling;

namespace RimAI.Core.Source.Modules.Orchestration
{
	internal sealed class OrchestrationService : IOrchestrationService
	{
		private readonly ILLMService _llm;
		private readonly IToolRegistryService _tooling;
		private readonly IToolMatchMode _classic;
		private readonly IToolMatchMode _narrow;

		public OrchestrationService(ILLMService llm, IToolRegistryService tooling)
		{
			_llm = llm;
			_tooling = tooling;
			_classic = new ClassicMode(tooling);
			_narrow = new NarrowTopKMode(tooling);
		}

		public async Task<ToolCallsResult> ExecuteAsync(string userInput, IReadOnlyList<string> participantIds, ToolOrchestrationOptions options, CancellationToken ct = default)
		{
			var profile = options?.Profile ?? ExecutionProfile.Fast;
			if (profile != ExecutionProfile.Fast)
			{
				return new ToolCallsResult { Mode = options?.Mode ?? OrchestrationMode.Classic, Profile = profile, IsSuccess = false, Error = "profile_not_implemented", ExposedTools = Array.Empty<string>(), DecidedCalls = Array.Empty<ToolCallRecord>(), Executions = Array.Empty<ToolExecutionRecord>(), TotalLatencyMs = 0 };
			}

			var mode = options?.Mode ?? OrchestrationMode.Classic;
			var start = DateTime.UtcNow;
			var toolsTuple = mode == OrchestrationMode.Classic
				? await _classic.GetToolsAsync(userInput, participantIds, mode, options, ct).ConfigureAwait(false)
				: await _narrow.GetToolsAsync(userInput, participantIds, mode, options, ct).ConfigureAwait(false);

			if (toolsTuple.toolsJson == null || toolsTuple.toolsJson.Count == 0)
			{
				return new ToolCallsResult { Mode = mode, Profile = profile, IsSuccess = false, Error = toolsTuple.error ?? "no_candidates", ExposedTools = Array.Empty<string>(), DecidedCalls = Array.Empty<ToolCallRecord>(), Executions = Array.Empty<ToolExecutionRecord>(), TotalLatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds };
			}

			// 构造一次非流式请求：system 限制仅 function_call；user 使用输入；tools 传入 Tool JSON 列表
			var convId = BuildConvKey(participantIds);
			var systemPrompt = "You are a function-calling planner. Decide the best tool to satisfy the user's request. Return exactly one tool call via tool_calls and nothing else. Do not output natural language.";
			// 先尝试失效会话缓存，避免命中无工具版本的缓存回复
			try { await _llm.InvalidateConversationCacheAsync(convId, ct).ConfigureAwait(false); } catch { }
			// 注意：为确保模型通过 function calling 返回 tool_calls，这里禁用 JSON 强制模式
			var resp = await _llm.GetResponseAsync(convId, systemPrompt, userInput ?? string.Empty, toolsTuple.toolsJson, jsonMode: false, cancellationToken: ct).ConfigureAwait(false);
			if (!resp.IsSuccess || resp.Value?.Message == null || resp.Value.Message.ToolCalls == null || resp.Value.Message.ToolCalls.Count == 0)
			{
				return new ToolCallsResult
				{
					Mode = mode,
					Profile = profile,
					IsSuccess = false,
					Error = "no_tool_calls",
					ExposedTools = ExtractToolNamesFromJson(toolsTuple.toolsJson),
					DecidedCalls = Array.Empty<ToolCallRecord>(),
					Executions = Array.Empty<ToolExecutionRecord>(),
					TotalLatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds
				};
			}

			var decided = new List<ToolCallRecord>();
			var execs = new List<ToolExecutionRecord>();
			var maxCalls = Math.Max(1, options?.MaxCalls ?? 1);
			foreach (var call in resp.Value.Message.ToolCalls.Take(maxCalls))
			{
				var callId = call.Id ?? Guid.NewGuid().ToString("N");
				var toolName = call.Function?.Name ?? string.Empty;
				Dictionary<string, object> args;
				try
				{
					var j = string.IsNullOrEmpty(call.Function?.Arguments) ? new JObject() : JObject.Parse(call.Function.Arguments);
					args = j.ToObject<Dictionary<string, object>>();
				}
				catch
				{
					args = new Dictionary<string, object>();
				}

				decided.Add(new ToolCallRecord { CallId = callId, ToolName = toolName, Args = args, GroupId = null, Order = decided.Count, DependsOn = Array.Empty<string>() });

				var t0 = DateTime.UtcNow;
				try
				{
					var result = await _tooling.ExecuteToolAsync(toolName, args, ct).ConfigureAwait(false);
					execs.Add(new ToolExecutionRecord
					{
						CallId = callId,
						GroupId = null,
						ToolName = toolName,
						Args = args,
						Outcome = "success",
						ResultObject = result,
						LatencyMs = (int)(DateTime.UtcNow - t0).TotalMilliseconds,
						Attempt = 1,
						StartedAtUtc = t0,
						FinishedAtUtc = DateTime.UtcNow
					});
				}
				catch (NotImplementedException)
				{
					execs.Add(new ToolExecutionRecord { CallId = callId, GroupId = null, ToolName = toolName, Args = args, Outcome = "invalid_name", ResultObject = null, LatencyMs = (int)(DateTime.UtcNow - t0).TotalMilliseconds, Attempt = 1, StartedAtUtc = t0, FinishedAtUtc = DateTime.UtcNow });
				}
				catch (OperationCanceledException)
				{
					execs.Add(new ToolExecutionRecord { CallId = callId, GroupId = null, ToolName = toolName, Args = args, Outcome = "timeout", ResultObject = null, LatencyMs = (int)(DateTime.UtcNow - t0).TotalMilliseconds, Attempt = 1, StartedAtUtc = t0, FinishedAtUtc = DateTime.UtcNow });
				}
				catch (Exception)
				{
					execs.Add(new ToolExecutionRecord { CallId = callId, GroupId = null, ToolName = toolName, Args = args, Outcome = "exception", ResultObject = null, LatencyMs = (int)(DateTime.UtcNow - t0).TotalMilliseconds, Attempt = 1, StartedAtUtc = t0, FinishedAtUtc = DateTime.UtcNow });
				}
			}

			var total = (int)(DateTime.UtcNow - start).TotalMilliseconds;
			LogSuccess(mode, profile, toolsTuple, decided.Count, execs.Count, total, convId);
			return new ToolCallsResult
			{
				Mode = mode,
				Profile = profile,
				IsSuccess = true,
				Error = null,
				ExposedTools = ExtractToolNamesFromJson(toolsTuple.toolsJson),
				DecidedCalls = decided,
				Executions = execs,
				TotalLatencyMs = total
			};
		}

		private static List<string> ExtractToolNamesFromJson(IReadOnlyList<string> toolsJson)
		{
			var list = new List<string>();
			if (toolsJson == null) return list;
			foreach (var j in toolsJson)
			{
				var name = ExtractName(j);
				if (!string.IsNullOrEmpty(name)) list.Add(name);
			}
			return list;
		}

		private static string ExtractName(string toolJson)
		{
			if (string.IsNullOrEmpty(toolJson)) return null;
			try
			{
				var jo = JObject.Parse(toolJson);
				var fn = jo["function"] as JObject;
				var n = fn?[(object)"name"]?.ToString();
				if (!string.IsNullOrEmpty(n)) return n;
				// 退化匹配：顶层 name/Name
				n = jo[(object)"name"]?.ToString() ?? jo[(object)"Name"]?.ToString();
				return string.IsNullOrWhiteSpace(n) ? null : n;
			}
			catch { }
			return null;
		}

		private static string BuildConvKey(IReadOnlyList<string> participantIds)
		{
			if (participantIds == null || participantIds.Count == 0) return "agent:stage";
			var list = participantIds.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
			list.Sort(StringComparer.Ordinal);
			return string.Join("|", list);
		}

		private static void LogSuccess(OrchestrationMode mode, ExecutionProfile profile, (IReadOnlyList<string> toolsJson, IReadOnlyList<(string name, double score)> scores, string error) tools, int decided, int executed, int totalMs, string convId)
		{
			try
			{
				var scoreSummary = OrchestrationLogging.SummarizeScores(tools.scores);
				System.Diagnostics.Debug.WriteLine($"[RimAI.Core][P5] ok mode={mode} profile={profile} exposed={tools.toolsJson?.Count ?? 0} decided={decided} executed={executed} scores={scoreSummary} conv={OrchestrationLogging.HashConv(convId)} totalMs={totalMs}");
			}
			catch { }
		}
	}
}


