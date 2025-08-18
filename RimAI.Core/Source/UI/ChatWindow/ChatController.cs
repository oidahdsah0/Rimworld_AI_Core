using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.UI.ChatWindow
{
	internal sealed class ChatController
	{
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;
		private readonly IWorldDataService _world;
		private readonly IOrchestrationService _orchestration;
		private readonly IPromptService _prompting;

		private static readonly ThreadLocal<Random> _rng = new ThreadLocal<Random>(() => new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));

		private CancellationTokenSource _streamCts;

		public ChatConversationState State { get; }

		public ChatController(
			ILLMService llm,
			IHistoryService history,
			IWorldDataService world,
			IOrchestrationService orchestration,
			IPromptService prompting,
			string convKey,
			IReadOnlyList<string> participantIds)
		{
			_llm = llm;
			_history = history;
			_world = world;
			_orchestration = orchestration;
			_prompting = prompting;
			State = new ChatConversationState
			{
				ConvKey = convKey,
				ParticipantIds = participantIds
			};
		}

		public async Task StartAsync()
		{
			try
			{
				// 记录参与者（若无则创建），并加载现有历史（若无则为空列表）
				await _history.UpsertParticipantsAsync(State.ConvKey, State.ParticipantIds).ConfigureAwait(false);
				var thread = await _history.GetThreadAsync(State.ConvKey, page: 1, pageSize: 200).ConfigureAwait(false);
				string playerName = "Player";
				try { playerName = await _world.GetPlayerNameAsync().ConfigureAwait(false) ?? "Player"; } catch { }
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						var msg = new ChatMessage
						{
							Id = e.Id ?? Guid.NewGuid().ToString("N"),
							Sender = e.Role == EntryRole.User ? MessageSender.User : MessageSender.Ai,
							DisplayName = e.Role == EntryRole.User ? (State.PlayerTitle ?? playerName) : "Pawn",
							TimestampUtc = e.Timestamp,
							Text = e.Content ?? string.Empty,
							IsCommand = false
						};
						State.PendingInitMessages.Enqueue(msg);
					}
				}
			}
			catch { }
		}

		public async Task SendSmalltalkAsync(string userText, CancellationToken ct = default)
		{
			CancelStreaming();
			_streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var linked = _streamCts.Token;
			// 预先清空上一次会话可能遗留的电路状态
			try { RimAI.Core.Source.Modules.LLM.LlmPolicies.ResetCircuit("stream:" + (State.ConvKey ?? "-")); } catch { }
			State.IsStreaming = true;
            // 新会话开始：复位 Finish 指示灯，并推进流式会话编号，屏蔽旧流的延迟包
            State.Indicators.FinishOn = false;
            unchecked { State.ActiveStreamId++; }
            int currentStreamId = State.ActiveStreamId;

			var userMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.User,
				DisplayName = State.PlayerTitle ?? (await _world.GetPlayerNameAsync(linked) ?? "Player"),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = false
			};
			State.Messages.Add(userMsg);

			var aiMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.Ai,
				DisplayName = await GetPawnDisplayNameAsync(linked),
				TimestampUtc = DateTime.UtcNow,
				Text = string.Empty,
				IsCommand = false
			};
			State.Messages.Add(aiMsg);

			// 真流式：仅 UI 允许
			_ = Task.Run(async () =>
			{
				try
				{
					var req = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = TryGetPawnLoadId(), IsCommand = false, Locale = null, UserInput = userText };
					var prompt = await _prompting.BuildAsync(req, linked).ConfigureAwait(false);
					SplitSpecialFromSystem(prompt.SystemPrompt, out var systemFiltered, out var specialLines);
					var systemPayload = BuildSystemPayload(systemFiltered, prompt.ContextBlocks);
					var messages = BuildMessagesArray(systemPayload, State.Messages);
					// 新版日志：仅输出 Messages 列表（system+历史+本次用户输入），遵循 UI 允许日志
					LogMessagesList(State.ConvKey, messages);
					var uiReq = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages, Stream = true };
					await foreach (var r in _llm.StreamResponseAsync(uiReq, linked))
					{
						if (!r.IsSuccess)
						{
							break;
						}
						// 忽略已被新会话替换的延迟包
						if (currentStreamId != State.ActiveStreamId) break;
						var chunk = r.Value;
						if (!string.IsNullOrEmpty(chunk.ContentDelta))
						{
							State.StreamingChunks.Enqueue(chunk.ContentDelta);
							var nowTick = DateTime.UtcNow;
							if (nowTick >= State.Indicators.DataNextAllowedBlinkUtc)
							{
								State.Indicators.DataOn = true;
								double factor = 0.7 + _rng.Value.NextDouble() * 0.6; // 0.7..1.3
								State.Indicators.DataBlinkUntilUtc = nowTick.AddMilliseconds(50 * factor);
								State.Indicators.DataNextAllowedBlinkUtc = nowTick.AddMilliseconds(20 * factor);
							}
						}
						if (!string.IsNullOrEmpty(chunk.FinishReason))
						{
							State.Indicators.FinishOn = true;
							break;
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception)
				{
				}
				finally
				{
					try { _streamCts?.Dispose(); } catch { }
					_streamCts = null;
					State.IsStreaming = false;
				}
			}, linked);
		}

		public async Task SendCommandAsync(string userText, CancellationToken ct = default)
		{
			CancelStreaming();
			_streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var linked = _streamCts.Token;
			// 预先清空上一次会话可能遗留的电路状态（编排路径不走流式，但保持一致性）
			try { RimAI.Core.Source.Modules.LLM.LlmPolicies.ResetCircuit("chat:" + (State.ConvKey ?? "-")); } catch { }
			State.IsStreaming = true;
            // 新会话开始：复位 Finish 指示灯，并推进流式会话编号，屏蔽旧流的延迟包
            State.Indicators.FinishOn = false;
            unchecked { State.ActiveStreamId++; }
            int currentStreamId = State.ActiveStreamId;

			var userMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.User,
				DisplayName = State.PlayerTitle ?? (await _world.GetPlayerNameAsync(linked) ?? "Player"),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = true
			};
			State.Messages.Add(userMsg);

			var aiMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.Ai,
				DisplayName = "Pawn",
				TimestampUtc = DateTime.UtcNow,
				Text = string.Empty,
				IsCommand = true
			};
			State.Messages.Add(aiMsg);

			_ = Task.Run(async () =>
			{
				try
				{
					// 段1：编排（非流式）——直接用原始用户输入参与工具决策
					var result = await _orchestration.ExecuteAsync(userText, State.ParticipantIds, new RimAI.Core.Source.Modules.Orchestration.ToolOrchestrationOptions
					{
						Mode = RimAI.Core.Source.Modules.Orchestration.OrchestrationMode.Classic,
						Profile = RimAI.Core.Source.Modules.Orchestration.ExecutionProfile.Fast,
						MaxCalls = 1
					}, linked);

					// 显示一次过程说明（PlanTrace 首条）到 UI（历史写入已由编排完成）
					if (result != null && result.PlanTrace != null && result.PlanTrace.Count > 0)
					{
						try
						{
							aiMsg.Text = result.PlanTrace[0] ?? string.Empty;
						}
						catch { }
					}

					// 构造 ExternalBlocks（RAG）注入工具结果概览
					var blocks = new List<ContextBlock>();
					if (result != null && result.Executions != null && result.Executions.Count > 0)
					{
						try
						{
							var title = string.IsNullOrWhiteSpace(result.HitDisplayName) ? "工具结果" : ("工具结果 · " + result.HitDisplayName);
							var compact = new List<object>();
							foreach (var e in result.Executions)
							{
								compact.Add(new { tool = e.ToolName, outcome = e.Outcome, result = e.ResultObject });
							}
							var text = JsonConvert.SerializeObject(compact);
							blocks.Add(new ContextBlock { Title = title, Text = text });
						}
						catch { }
					}

					// 段2：RAG→LLM 真流式
					var req2 = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = TryGetPawnLoadId(), IsCommand = true, Locale = null, UserInput = userText, ExternalBlocks = blocks };
					var prompt2 = await _prompting.BuildAsync(req2, linked).ConfigureAwait(false);
					SplitSpecialFromSystem(prompt2.SystemPrompt, out var systemFiltered3, out var specialLines3);
					var systemPayload2 = BuildSystemPayload(systemFiltered3, prompt2.ContextBlocks);
					var messages2 = BuildMessagesArray(systemPayload2, State.Messages);
					LogMessagesList(State.ConvKey, messages2);
					var uiReq2 = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages2, Stream = true };
					await foreach (var r in _llm.StreamResponseAsync(uiReq2, linked))
					{
						if (!r.IsSuccess) { break; }
						if (currentStreamId != State.ActiveStreamId) break;
						var chunk = r.Value;
						if (!string.IsNullOrEmpty(chunk.ContentDelta))
						{
							State.StreamingChunks.Enqueue(chunk.ContentDelta);
							var nowTick = DateTime.UtcNow;
							if (nowTick >= State.Indicators.DataNextAllowedBlinkUtc)
							{
								State.Indicators.DataOn = true;
								double factor = 0.7 + _rng.Value.NextDouble() * 0.6;
								State.Indicators.DataBlinkUntilUtc = nowTick.AddMilliseconds(120 * factor);
								State.Indicators.DataNextAllowedBlinkUtc = nowTick.AddMilliseconds(160 * factor);
							}
						}
						if (!string.IsNullOrEmpty(chunk.FinishReason))
						{
							State.Indicators.FinishOn = true;
							break;
						}
					}
				}
				catch (OperationCanceledException)
				{
				}
				catch (Exception)
				{
				}
				finally
				{
					try { _streamCts?.Dispose(); } catch { }
					_streamCts = null;
					State.IsStreaming = false;
				}
			}, linked);
		}

		public void CancelStreaming()
		{
			// 标记会话为新流并先取消旧 CTS，然后短延迟，避免尾包竞争
			unchecked { State.ActiveStreamId++; }
			try { _streamCts?.Cancel(); } catch { }
			// 若仍在流式，删除最后一次用户发言及半生成的 AI，并把文本归还输入框；清空剩余 chunk
			if (State.IsStreaming)
			{
				// 1) 使旧流失效（已在方法开头推进 ActiveStreamId）
				// 2) 删除最后一条 AI 消息（半生成）
				for (int i = State.Messages.Count - 1; i >= 0; i--)
				{
					if (State.Messages[i].Sender == MessageSender.Ai)
					{
						State.Messages.RemoveAt(i);
						break;
					}
				}
				// 3) 删除最后一条用户消息，并暂存文本
				string lastUserText = null;
				for (int i = State.Messages.Count - 1; i >= 0; i--)
				{
					if (State.Messages[i].Sender == MessageSender.User)
					{
						lastUserText = State.Messages[i].Text;
						State.Messages.RemoveAt(i);
						break;
					}
				}
				if (!string.IsNullOrEmpty(lastUserText))
				{
					State.LastUserInputStash = lastUserText;
				}
				// 4) 清空未消费的 chunk，复位指示灯与状态
				while (State.StreamingChunks.TryDequeue(out _)) { }
				State.Indicators.DataOn = false;
				State.Indicators.FinishOn = false;
				State.IsStreaming = false;
				// 5) 失效 Framework 会话缓存（防止上游缓存导致下一次响应延迟）；避免在 UI 线程阻塞
				_ = Task.Run(async () => { try { await _llm.InvalidateConversationCacheAsync(State.ConvKey); } catch { } });
			}
		}

		public bool TryDequeueChunk(out string chunk)
		{
			return State.StreamingChunks.TryDequeue(out chunk);
		}

		public async Task WriteFinalToHistoryIfAnyAsync()
		{
			var final = GetLastAiText();
			var lastUser = FindLast(State.Messages, MessageSender.User);
			if (lastUser != null && !string.IsNullOrEmpty(final))
			{
				await _history.AppendPairAsync(State.ConvKey, lastUser.Text, final);
			}
		}

		public string GetLastAiText()
		{
			for (var i = State.Messages.Count - 1; i >= 0; i--)
			{
				var m = State.Messages[i];
				if (m.Sender == MessageSender.Ai)
				{
					return m.Text ?? string.Empty;
				}
			}
			return string.Empty;
		}

		private static ChatMessage FindLast(List<ChatMessage> list, MessageSender who)
		{
			for (var i = list.Count - 1; i >= 0; i--)
			{
				if (list[i].Sender == who) return list[i];
			}
			return null;
		}

		private static IEnumerable<string> SliceForPseudoStream(string text, int chunkChars)
		{
			if (string.IsNullOrEmpty(text)) yield break;
			var idx = 0;
			while (idx < text.Length)
			{
				var len = Math.Min(chunkChars, text.Length - idx);
				yield return text.Substring(idx, len);
				idx += len;
			}
		}

		private int? TryGetPawnLoadId()
		{
			try
			{
				foreach (var id in State.ParticipantIds)
				{
					if (id != null && id.StartsWith("pawn:"))
					{
						var s = id.Substring("pawn:".Length);
						if (int.TryParse(s, out var v)) return v;
					}
				}
			}
			catch { }
			return null;
		}

		private async Task<string> GetPawnDisplayNameAsync(CancellationToken ct)
		{
			try
			{
				var id = TryGetPawnLoadId();
				if (id.HasValue)
				{
					var snap = await _world.GetPawnPromptSnapshotAsync(id.Value, ct).ConfigureAwait(false);
					var name = snap?.Id?.Name;
					if (!string.IsNullOrWhiteSpace(name)) return name;
				}
			}
			catch { }
			return "Pawn";
		}

		private static void SplitSpecialFromSystem(string systemPrompt, out string filtered, out System.Collections.Generic.List<string> special)
		{
			// 不再过滤“职务”等行，直接保留原样
			filtered = systemPrompt ?? string.Empty;
			special = new System.Collections.Generic.List<string>();
		}

		private static string BuildSystemPayload(string systemFiltered, System.Collections.Generic.IReadOnlyList<RimAI.Core.Source.Modules.Prompting.Models.ContextBlock> blocks)
		{
			var sb = new System.Text.StringBuilder();
			if (!string.IsNullOrEmpty(systemFiltered)) sb.AppendLine(systemFiltered);
			// 仅拼接 SystemPrompt，不拼接 Activities 到系统段；遵循新 API：系统段包含各项系统提示行
			return sb.ToString().TrimEnd();
		}

		private static System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage> BuildMessagesArray(string systemPayload, System.Collections.Generic.IReadOnlyList<ChatMessage> visibleMessages)
		{
			var list = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>();
			if (!string.IsNullOrWhiteSpace(systemPayload)) list.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = systemPayload });
			if (visibleMessages != null)
			{
				foreach (var m in visibleMessages)
				{
					var role = m.Sender == MessageSender.User ? "user" : "assistant";
					var content = m.Text ?? string.Empty;
					if (string.IsNullOrWhiteSpace(content)) continue; // 跳过空占位
					list.Add(new RimAI.Framework.Contracts.ChatMessage { Role = role, Content = content });
				}
			}
			return list;
		}

		private static void LogMessagesList(string convKey, System.Collections.Generic.IReadOnlyList<RimAI.Framework.Contracts.ChatMessage> messages)
		{
			try
			{
				var sb = new System.Text.StringBuilder();
				sb.AppendLine("[RimAI.Core][P10] ChatUI Outbound Messages");
				sb.AppendLine("conv=" + convKey);
				if (messages != null)
				{
					for (int i = 0; i < messages.Count; i++)
					{
						var m = messages[i];
						sb.AppendLine($"[{i}] {m?.Role}: {m?.Content}");
					}
				}
				Verse.Log.Message(sb.ToString());
			}
			catch { }
		}

		private static void LogBuiltPrompt(string convKey, string systemFiltered, System.Collections.Generic.IReadOnlyList<string> special, PromptBuildResult prompt, string mode)
		{
			try
			{
				var sb = new System.Text.StringBuilder();
				sb.AppendLine($"[RimAI.Core][P10] ChatUI Prompt ({mode})");
				sb.AppendLine($"conv={convKey}");
				sb.AppendLine("--- SystemPrompt ---");
				sb.AppendLine(systemFiltered ?? string.Empty);
				if (special != null && special.Count > 0)
				{
					sb.AppendLine("--- Special Info ---");
					for (int i = 0; i < special.Count; i++) sb.AppendLine(special[i]);
					sb.AppendLine();
				}
				sb.AppendLine("--- Activities ---");
				if (prompt?.ContextBlocks != null)
				{
					foreach (var b in prompt.ContextBlocks)
					{
						var title = b?.Title;
						var text = b?.Text;
						bool textIsSingleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
						if (!string.IsNullOrWhiteSpace(title) && textIsSingleLine)
						{
							sb.AppendLine(title + " " + text);
						}
						else
						{
							if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
							if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
						}
						sb.AppendLine();
					}
				}
				sb.AppendLine("--- UserPrefixedInput ---");
				sb.AppendLine(prompt?.UserPrefixedInput ?? string.Empty);
				Verse.Log.Message(sb.ToString());
			}
			catch { }
		}

		private static void LogOutboundRequest(string convKey, ChatMessage userMsg, ChatMessage aiMsg, PromptBuildResult prompt, string finalUserText, string mode)
		{
			try
			{
				var header = $"[RimAI.Core][P10] Outbound ({mode})";
				var title = userMsg?.DisplayName ?? "Player";
				var ts = (userMsg?.TimestampUtc ?? DateTime.UtcNow).ToLocalTime().ToString("HH:mm:ss");
				var content = userMsg?.Text ?? string.Empty;
				var line1 = $"{title} {ts}: {content}";
				var line2 = $"“{title}”发来的最新内容：{content}";
				Verse.Log.Message(header + "\n" + line1 + "\n\n" + line2);
			}
			catch { }
		}

		private static string ComposeUserMessage(PromptBuildResult prompt, System.Collections.Generic.IReadOnlyList<string> special)
		{
			if (prompt == null) return string.Empty;
			var sb = new System.Text.StringBuilder();
			if (special != null && special.Count > 0)
			{
				sb.AppendLine("--- Special Info ---");
				foreach (var s in special) sb.AppendLine(s);
				sb.AppendLine();
			}
			if (prompt.ContextBlocks != null)
			{
				if (prompt.ContextBlocks.Count > 0)
				{
					sb.AppendLine("--- Activities ---");
				}
				foreach (var b in prompt.ContextBlocks)
				{
					var title = b?.Title;
					var text = b?.Text;
					bool textIsSingleLine = !string.IsNullOrWhiteSpace(text) && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0;
					if (!string.IsNullOrWhiteSpace(title) && textIsSingleLine)
					{
						sb.AppendLine(title + " " + text);
					}
					else
					{
						if (!string.IsNullOrWhiteSpace(title)) sb.AppendLine(title);
						if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
					}
					if (sb.Length > 0) sb.AppendLine();
				}
			}
			sb.Append(prompt.UserPrefixedInput ?? string.Empty);
			return sb.ToString();
		}

		private static string ExtractTextFromOrchestrationResult(RimAI.Core.Source.Modules.Orchestration.ToolCallsResult result)
		{
			if (result == null) return string.Empty;
			if (result.Executions == null || result.Executions.Count == 0) return string.Empty;
			var sb = new StringBuilder();
			foreach (var e in result.Executions)
			{
				if (e.ResultObject != null) sb.AppendLine(e.ResultObject.ToString());
			}
			return sb.ToString().Trim();
		}

		private static string FormatLatestLine(string displayName, DateTime timestamp, string text)
		{
			var ts = timestamp.ToLocalTime().ToString("HH:mm:ss");
			return $"{displayName} {ts}: {text}";
		}
	}
}


