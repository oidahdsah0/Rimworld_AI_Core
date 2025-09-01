using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Orchestration.Modes;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.Modules.Orchestration
{
	internal sealed class OrchestrationService : IOrchestrationService
	{
		private readonly ILLMService _llm;
		private readonly IToolRegistryService _tooling;
		private readonly IToolMatchMode _classic;
		private readonly IToolMatchMode _narrow;
		private readonly IHistoryService _history;
		private readonly ILocalizationService _loc;
        private readonly IWorldDataService _world;

		public OrchestrationService(ILLMService llm, IToolRegistryService tooling, IHistoryService history, ILocalizationService localization, IWorldDataService world)
		{
			_llm = llm;
			_tooling = tooling;
			_classic = new ClassicMode(tooling);
			_narrow = new NarrowTopKMode(tooling);
			_history = history;
			_loc = localization;
            _world = world;
		}

		public async Task<ToolCallsResult> ExecuteAsync(string userInput, IReadOnlyList<string> participantIds, ToolOrchestrationOptions options, CancellationToken ct = default)
		{
			var profile = options?.Profile ?? ExecutionProfile.Fast;
			if (profile != ExecutionProfile.Fast)
			{
				return new ToolCallsResult { Mode = options?.Mode ?? OrchestrationMode.Classic, Profile = profile, IsSuccess = false, Error = "profile_not_implemented", ExposedTools = Array.Empty<string>(), DecidedCalls = Array.Empty<ToolCallRecord>(), Executions = Array.Empty<ToolExecutionRecord>(), TotalLatencyMs = 0 };
			}

			var mode = options?.Mode ?? OrchestrationMode.Classic;
			// 若模式为 NarrowTopK，但当前 TopK 不可用（Embedding 关闭），则返回明确错误，不自动降级
			if (mode == OrchestrationMode.NarrowTopK)
			{
				try
				{
					var topkAvailable = (_tooling as RimAI.Core.Source.Modules.Tooling.IToolRegistryService)?.IsTopKAvailable() ?? false;
					if (!topkAvailable)
					{
						return new ToolCallsResult { Mode = mode, Profile = profile, IsSuccess = false, Error = "embedding_disabled", ExposedTools = Array.Empty<string>(), DecidedCalls = Array.Empty<ToolCallRecord>(), Executions = Array.Empty<ToolExecutionRecord>(), TotalLatencyMs = 0 };
					}
				}
				catch { }
			}
			var start = DateTime.UtcNow;
			// 统一注入“最大工具等级”：混合参与者时取最大等级为门槛（pawn 视为 Lv1；server/thing 查询真实等级）。
			var injected = options ?? new ToolOrchestrationOptions();
			try
			{
				int computedMax = 1; // 基线
				var include = new List<string>();
				if (participantIds != null)
				{
					foreach (var pid in participantIds)
					{
						if (string.IsNullOrWhiteSpace(pid)) continue;
						var id = pid.Trim();
						if (id.StartsWith("pawn:", StringComparison.OrdinalIgnoreCase))
						{
							computedMax = Math.Max(computedMax, 1);
							continue;
						}
						if (id.StartsWith("server:", StringComparison.OrdinalIgnoreCase) || id.StartsWith("thing:", StringComparison.OrdinalIgnoreCase))
						{
							try
							{
								// 解析 server/thing 的数值 ID 并查询服务器等级
								string s = id.StartsWith("thing:", StringComparison.OrdinalIgnoreCase) ? id.Substring(6) : id.Substring(7);
								if (int.TryParse(s, out var thingId) && _world != null)
								{
									var lv = await _world.GetAiServerLevelAsync(thingId, ct).ConfigureAwait(false);
									lv = Math.Max(1, Math.Min(3, lv));
									computedMax = Math.Max(computedMax, lv);
									// 构建工具槽白名单（仅服务器命令模式下需要）
									try
									{
										var server = RimAI.Core.Source.Boot.RimAICoreMod.Container.Resolve<RimAI.Core.Source.Modules.Server.IServerService>();
										var entityId = $"thing:{thingId}";
										var slots = server?.GetSlots(entityId);
										if (slots != null)
										{
											foreach (var sl in slots)
											{
												if (sl != null && sl.Enabled && !string.IsNullOrWhiteSpace(sl.ToolName)) include.Add(sl.ToolName);
											}
										}
									}
									catch { }
								}
							}
							catch { }
						}
					}
				}
				// 注入最终门槛（1..3）
				injected.MaxToolLevel = Math.Max(1, Math.Min(3, computedMax));
				// 若存在服务器参与者，则透传“工具槽白名单”以限制命令模式仅能调用已装载工具
				try
				{
					if (include.Count > 0)
					{
						var distinct = new HashSet<string>(include, StringComparer.OrdinalIgnoreCase);
						injected.IncludeWhitelist = new List<string>(distinct);
					}
				}
				catch { }
			}
			catch { }
			var toolsTuple = mode == OrchestrationMode.Classic
				? await _classic.GetToolsAsync(userInput, participantIds, mode, injected, ct).ConfigureAwait(false)
				: await _narrow.GetToolsAsync(userInput, participantIds, mode, injected, ct).ConfigureAwait(false);

			if (toolsTuple.toolsJson == null || toolsTuple.toolsJson.Count == 0)
			{
				return new ToolCallsResult { Mode = mode, Profile = profile, IsSuccess = false, Error = toolsTuple.error ?? "no_candidates", ExposedTools = Array.Empty<string>(), DecidedCalls = Array.Empty<ToolCallRecord>(), Executions = Array.Empty<ToolExecutionRecord>(), TotalLatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds };
			}

			// 构造一次非流式请求：System+Messages（对齐 API），并附工具列表
			var convId = BuildConvKey(participantIds);
			var systemPrompt = "You are a function-calling planner. Decide the best tool to satisfy the user's request. Return exactly one tool call via tool_calls and nothing else. Do not output natural language.";
			// 先尝试失效会话缓存，避免命中无工具版本的缓存回复
			try { await _llm.InvalidateConversationCacheAsync(convId, ct).ConfigureAwait(false); } catch { }
			// 注意：为确保模型通过 function calling 返回 tool_calls，这里禁用 JSON 强制模式
			var messages = new List<RimAI.Framework.Contracts.ChatMessage>
			{
				new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = systemPrompt }
			};
			bool currentUserAlreadyInHistory = false;
			try
			{
				var historyEntries = await _history.GetAllEntriesAsync(convId, ct).ConfigureAwait(false);
				if (historyEntries != null)
				{
					// 先按时间顺序回灌历史（已是升序）
					foreach (var e in historyEntries)
					{
						var role = e.Role == EntryRole.User ? "user" : "assistant";
						var content = e.Content ?? string.Empty;
						if (string.IsNullOrWhiteSpace(content)) continue;
						messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = role, Content = content });
					}

					// 检查历史中最后一条用户发言是否与当前输入相同，避免重复追加
					try
					{
						var lastUserInHistory = historyEntries
							.AsEnumerable()
							.Reverse()
							.FirstOrDefault(x => x != null && x.Role == EntryRole.User && !string.IsNullOrWhiteSpace(x.Content))?.Content;
						var a = (lastUserInHistory ?? string.Empty).Trim();
						var b = (userInput ?? string.Empty).Trim();
						currentUserAlreadyInHistory = !string.IsNullOrEmpty(b) && string.Equals(a, b, StringComparison.Ordinal);
					}
					catch { }
				}
			}
			catch { }
			// 若用户输入已由 UI 写入历史，则不再追加重复的 user 消息
			if (!(currentUserAlreadyInHistory))
			{
				messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "user", Content = userInput ?? string.Empty });
			}
			var req = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = convId, Messages = messages, Stream = false };
			var resp = await _llm.GetResponseAsync(req, toolsTuple.toolsJson, jsonMode: false, cancellationToken: ct).ConfigureAwait(false);
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
					PlanTrace = Array.Empty<string>(),
					HitDisplayName = string.Empty,
					TotalLatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds
				};
			}

			var decided = new List<ToolCallRecord>();
			var execs = new List<ToolExecutionRecord>();
			var plan = new List<string>();
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

				// 命中显示名与过程文案（只针对首个命中生成一条）
				if (decided.Count == 1)
				{
					var disp = _tooling.GetToolDisplayNameOrNull(toolName) ?? toolName;
					try
					{
						var locale = options?.Locale ?? _loc?.GetDefaultLocale() ?? "en";
						var key = "tool.display." + (toolName ?? string.Empty);
						var localized = _loc?.Get(locale, key, disp) ?? disp;
						disp = string.IsNullOrWhiteSpace(localized) ? disp : localized;
					}
					catch { }
					var pawnName = "Pawn";
					try
					{
						// 仅为叙述用途，从 participantIds 粗略解析 pawn:xxx
						pawnName = "Pawn";
					}
					catch { }
					var line = _loc?.Format(options?.Locale ?? _loc?.GetDefaultLocale() ?? "en", "orchestration.plantrace.hit_tool", new System.Collections.Generic.Dictionary<string, string>
					{
						{ "pawn", pawnName },
						{ "displayName", disp }
					}, $"{pawnName} used the handheld device to open an app: {disp}. The results are ready.") ?? $"{pawnName} used the handheld device to open an app: {disp}. The results are ready.";
					if (!string.IsNullOrWhiteSpace(line))
					{
						plan.Add(line);
						try { await _history.AppendRecordAsync(convId, "ChatUI", "agent:stage", "chat", line, advanceTurn: false, ct: ct).ConfigureAwait(false); } catch { }
					}
				}

				var t0 = DateTime.UtcNow;
				try
				{
					// 注入由编排层计算的最大可用工具等级，供工具在执行期进行向下兼容的等级核对
					if (args == null) args = new Dictionary<string, object>();
					try { args["server_level"] = injected?.MaxToolLevel ?? 1; } catch { args["server_level"] = 1; }
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
					// P14：工具调用轨迹（tool_call）在命令模式下不写入历史，避免占用空间
					try
					{
						if (!(options?.IsCommand == true))
						{
							string compact = result?.ToString() ?? string.Empty;
							await _history.AppendRecordAsync(convId, "ChatUI", $"tool:{toolName}", "tool_call", compact, advanceTurn: false, ct: ct).ConfigureAwait(false);
						}
					}
					catch { }
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
			// 计算命中工具的显示名（本地化后）
			string hitName = string.Empty;
			if (decided.Count > 0)
			{
				var nm = decided[0].ToolName ?? string.Empty;
				var baseName = _tooling.GetToolDisplayNameOrNull(nm) ?? nm;
				try
				{
					var locale = options?.Locale ?? _loc?.GetDefaultLocale() ?? "en";
					var key = "tool.display." + nm;
					var localized = _loc?.Get(locale, key, baseName) ?? baseName;
					hitName = string.IsNullOrWhiteSpace(localized) ? baseName : localized;
				}
				catch { hitName = baseName; }
			}

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
				PlanTrace = plan,
				HitDisplayName = hitName,
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


