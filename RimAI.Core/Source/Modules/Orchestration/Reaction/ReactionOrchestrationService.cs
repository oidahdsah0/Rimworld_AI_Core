using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Tooling;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.Modules.Orchestration.Reaction
{
	internal sealed class ReactionOrchestrationService : IReactionOrchestrationService
	{
		private readonly ILLMService _llm;
		private readonly ILocalizationService _loc;
		private readonly IHistoryService _history;
		private readonly IPromptService _prompt;
		private readonly IToolRegistryService _tooling;

		private readonly ConcurrentQueue<Func<CancellationToken, Task>> _queue = new();
		private int _workerStarted = 0;

		public ReactionOrchestrationService(ILLMService llm, ILocalizationService loc, IHistoryService history, IPromptService prompt, IToolRegistryService tooling)
		{
			_llm = llm; _loc = loc; _history = history; _prompt = prompt; _tooling = tooling;
		}

		public Task EnqueuePawnSmalltalkReactionAsync(string convKey, IReadOnlyList<string> participantIds, string lastUserText, string lastAssistantText, string locale, string playerTitle, CancellationToken ct = default)
		{
			_queue.Enqueue(async token =>
			{
				try
				{
					await ExecuteOnceAsync(convKey, participantIds, lastUserText, lastAssistantText, locale, playerTitle, token).ConfigureAwait(false);
				}
				catch { }
			});
			EnsureWorker();
			return Task.CompletedTask;
		}

		private void EnsureWorker()
		{
			if (Interlocked.Exchange(ref _workerStarted, 1) == 1) return;
			_ = Task.Run(async () =>
			{
				while (true)
				{
					if (_queue.TryDequeue(out var work))
					{
						try { await work(CancellationToken.None).ConfigureAwait(false); }
						catch { }
					}
					else
					{
						await Task.Delay(250).ConfigureAwait(false);
					}
				}
			});
		}

		private async Task ExecuteOnceAsync(string convKey, IReadOnlyList<string> participantIds, string lastUserText, string lastAssistantText, string locale, string playerTitle, CancellationToken ct)
		{
			// 仅在参与者中存在 pawn: 且不存在 server:/thing: 时触发
			bool hasPawn = participantIds?.Any(id => id != null && id.StartsWith("pawn:")) == true;
			bool hasServer = participantIds?.Any(id => id != null && (id.StartsWith("server:") || id.StartsWith("thing:"))) == true;
			if (!hasPawn || hasServer) return;

			// 冷却预检：若主要 pawn 处于反应冷却，则跳过派发，避免无谓请求
			try
			{
				int pawnId = -1;
				foreach (var id in participantIds ?? Array.Empty<string>())
				{
					if (string.IsNullOrWhiteSpace(id)) continue;
					if (id.StartsWith("pawn:") && pawnId < 0)
					{
						var tail = id.Substring("pawn:".Length);
						if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) { pawnId = parsed; break; }
					}
				}
				if (pawnId > 0 && RimAI.Core.Source.Modules.Tooling.Execution.ReactionCooldown.IsCooling(pawnId))
				{
					try { Verse.Log.Message($"[RimAI.Core][Reaction] Cooldown pre-check active for pawn {pawnId}, skip dispatch."); } catch { }
					return;
				}
			}
			catch { }

			// 本地化提示
			var useLocale = locale ?? _loc?.GetDefaultLocale() ?? "zh-Hans";
			string sys = _loc?.Get(useLocale, "prompt.reaction.system", "After the chat, return a function call with mood_delta and mood_title only.") ?? "After the chat, return a function call with mood_delta and mood_title only.";
			// 可选：将示例拼接进 System，增强分布多样性
			try
			{
				var examples = _loc?.Get(useLocale, "prompt.reaction.examples", null);
				if (!string.IsNullOrWhiteSpace(examples))
				{
					sys = sys + "\n\n[examples]\n" + examples.Trim();
				}
			}
			catch { }

			var messages = new List<RimAI.Framework.Contracts.ChatMessage>
			{
				new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = sys },
				new RimAI.Framework.Contracts.ChatMessage { Role = "user", Content = BuildUserPayload(locale, playerTitle, lastUserText, lastAssistantText) }
			};

			// 仅暴露反应工具（Classic，全量里会被过滤到 Lv<=3）
			var opts = new RimAI.Core.Source.Modules.Tooling.ToolQueryOptions { IncludeWhitelist = new[] { "pawn_conversation_reaction" } };
			var toolsTuple = _tooling.GetClassicToolCallSchema(opts);
			var tools = toolsTuple?.ToolsJson ?? Array.Empty<string>();
			if (tools.Count == 0) return;

			// 清理会话缓存，避免受到主聊天上下文影响
			try { await _llm.InvalidateConversationCacheAsync(convKey, ct).ConfigureAwait(false); } catch { }
			var req = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = convKey, Messages = messages, Stream = false };
			// 诊断：发起请求日志（不含敏感头）
			try { Verse.Log.Message($"[RimAI.Core][Reaction] Dispatch NonStream conv={convKey} msgs={messages.Count} tools={tools.Count}"); } catch { }
			var resp = await _llm.GetResponseAsync(req, tools, jsonMode: false, cancellationToken: ct).ConfigureAwait(false);
			if (!resp.IsSuccess || resp.Value?.Message == null || resp.Value.Message.ToolCalls == null || resp.Value.Message.ToolCalls.Count == 0) return;
			// 诊断：LLM 返回内容（仅摘要）
			try
			{
				var tc = resp.Value.Message.ToolCalls;
				var count = tc?.Count ?? 0;
				string firstName = count > 0 ? (tc[0]?.Function?.Name ?? "") : "";
				string firstArgs = count > 0 ? (tc[0]?.Function?.Arguments ?? "") : "";
				if (firstArgs.Length > 1024) firstArgs = firstArgs.Substring(0, 1024) + "...";
				// Verse.Log.Message($"[RimAI.Core][Reaction] LLM tool_calls count={count}, first={{name={firstName}, args={firstArgs}}}");
			}
			catch { }

			var call = resp.Value.Message.ToolCalls[0];
			string callId = call?.Id ?? Guid.NewGuid().ToString("N");
			string toolName = call?.Function?.Name ?? string.Empty;
			if (!string.Equals(toolName, "pawn_conversation_reaction", StringComparison.OrdinalIgnoreCase)) return;
			Dictionary<string, object> args;
			try { var j = string.IsNullOrEmpty(call.Function?.Arguments) ? new JObject() : JObject.Parse(call.Function.Arguments); args = j.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>(); }
			catch { args = new Dictionary<string, object>(); }

			// 注入上下文供执行器使用（不会暴露给模型）
			try
			{
				args["conv_key"] = convKey;
				args["participant_ids"] = participantIds?.ToArray() ?? Array.Empty<string>();
				args["locale"] = locale ?? string.Empty;
				args["player_title"] = playerTitle ?? string.Empty;
				args["last_user_text"] = lastUserText ?? string.Empty;
				args["last_assistant_text"] = lastAssistantText ?? string.Empty;
			}
			catch { }

			// 不再写入任何与“心情反应”相关的历史记录，避免在会话历史中出现请求/结果 JSON
			//（此前这里会写入一个轻量的 tool_call 记录用于调试）

			try
			{
				var result = await _tooling.ExecuteToolAsync(toolName, args, ct).ConfigureAwait(false);
				// try { Verse.Log.Message($"[RimAI.Core][Reaction] Executed tool '{toolName}' result={SafeToString(result)}"); } catch { }
			}
			catch (Exception ex)
			{
				try { Verse.Log.Warning($"[RimAI.Core][Reaction] Tool execution failed: {ex.Message}"); } catch { }
			}
		}

		private static string SafeToString(object o)
		{
			if (o == null) return "<null>";
			try
			{
				var s = Newtonsoft.Json.JsonConvert.SerializeObject(o);
				if (s.Length > 1024) s = s.Substring(0, 1024) + "...";
				return s;
			}
			catch { return o.ToString(); }
		}

		private static string BuildUserPayload(string locale, string playerTitle, string userText, string aiText)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("[context]");
			sb.AppendLine("locale=" + (locale ?? ""));
			sb.AppendLine("player_title=" + (playerTitle ?? ""));
			sb.AppendLine();
			sb.AppendLine("[last_user_text]");
			sb.AppendLine(userText ?? string.Empty);
			sb.AppendLine();
			sb.AppendLine("[last_assistant_text]");
			sb.AppendLine(aiText ?? string.Empty);
			return sb.ToString();
		}
	}
}
