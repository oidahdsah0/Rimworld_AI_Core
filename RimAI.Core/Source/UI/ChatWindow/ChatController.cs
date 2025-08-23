using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;
using Verse;

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
			// try { Verse.Log.Message($"[RimAI.Core][P10] ChatController ctor conv={convKey} pids={participantIds?.Count ?? 0}"); } catch { }
		}

		public async Task StartAsync()
		{
			try
			{
				// var t0 = DateTime.UtcNow;
				// Verse.Log.Message($"[RimAI.Core][P10] ChatController.StartAsync begin conv={State?.ConvKey}");
				// 记录参与者（若无则创建），并加载现有历史（若无则为空列表）
				await _history.UpsertParticipantsAsync(State.ConvKey, State.ParticipantIds).ConfigureAwait(false);
				// 为避免工具 JSON 污染对话，在 ChatUI 载入时使用原始历史并过滤 type=tool_call
				var rawList = await _history.GetAllEntriesRawAsync(State.ConvKey).ConfigureAwait(false);
				string playerName = "RimAI.Common.Player".Translate().ToString();
				try { playerName = await _world.GetPlayerNameAsync().ConfigureAwait(false) ?? "RimAI.Common.Player".Translate().ToString(); } catch { }
				if (rawList != null)
				{
					foreach (var r in rawList)
					{
						if (r == null || r.Deleted) continue;
						// 解析 JSON payload，若 type=tool_call 则跳过
						string displayText = r.Content ?? string.Empty;
						try
						{
							var jo = JObject.Parse(r.Content ?? "{}");
							var type = jo.Value<string>("type") ?? string.Empty;
							if (string.Equals(type, "tool_call", System.StringComparison.OrdinalIgnoreCase)) continue;
							displayText = jo.Value<string>("content") ?? displayText;
						}
						catch { }
						// 计算显示名：用户采用当前称谓或玩家名；AI 采用实际小人名
						string displayName = r.Role == EntryRole.User ? (State.PlayerTitle ?? playerName) : "RimAI.Common.Pawn".Translate().ToString();
						if (r.Role != EntryRole.User)
						{
							try
							{
								var nm = await GetPawnDisplayNameAsync(System.Threading.CancellationToken.None).ConfigureAwait(false);
								if (!string.IsNullOrWhiteSpace(nm)) displayName = nm;
							}
							catch { }
						}
						var msg = new ChatMessage
						{
							Id = r.Id ?? Guid.NewGuid().ToString("N"),
							Sender = r.Role == EntryRole.User ? MessageSender.User : MessageSender.Ai,
							DisplayName = displayName,
							TimestampUtc = r.Timestamp,
							Text = displayText,
							IsCommand = false
						};
						State.PendingInitMessages.Enqueue(msg);
					}
				}
				// try { var ms = (int)(DateTime.UtcNow - t0).TotalMilliseconds; Verse.Log.Message($"[RimAI.Core][P10] ChatController.StartAsync done msgs={State.PendingInitMessages?.Count} elapsed={ms}ms"); } catch { }
			}
			catch { }
		}

		public async Task SendSmalltalkAsync(string userText, CancellationToken ct = default)
		{
			// try { Verse.Log.Message($"[RimAI.Core][P10] SendSmalltalk begin len={userText?.Length ?? 0}"); } catch { }
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
				DisplayName = State.PlayerTitle ?? (await _world.GetPlayerNameAsync(linked) ?? "RimAI.Common.Player".Translate().ToString()),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = false
			};
			State.Messages.Add(userMsg);
			// P14：立即写入用户消息（不推进回合）
			try
			{
				var playerId = GetPlayerIdOrNull() ?? "player:unknown";
				await _history.AppendRecordAsync(State.ConvKey, "ChatUI", playerId, "chat", userText ?? string.Empty, advanceTurn: false, ct: linked).ConfigureAwait(false);
			}
			catch { }

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
					// 输出 Messages 列表（system+历史+本次用户输入）到日志
					LogMessagesList(State.ConvKey, messages);
					var uiReq = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages, Stream = true };
					await foreach (var r in _llm.StreamResponseAsync(uiReq, linked))
					{
						if (!r.IsSuccess) { break; }
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
					// 流完成后：在控制器侧统一合并残余分片并尝试写入最终历史（独立于 UI 是否在绘制）
					try { await TryFinalizeStreamingAndCommitAsync().ConfigureAwait(false); } catch { }
				}
				catch (OperationCanceledException) { }
				catch (Exception) { }
				finally
				{
					try { _streamCts?.Dispose(); } catch { }
					_streamCts = null;
					State.IsStreaming = false;
					// 中断情况下，确保指示灯复位，避免 UI 假性“忙碌”
					State.Indicators.DataOn = false;
					State.Indicators.FinishOn = false;
				}
			}, linked);
		}

		public async Task SendCommandAsync(string userText, CancellationToken ct = default)
		{
			// try { Verse.Log.Message($"[RimAI.Core][P10] SendCommand begin len={userText?.Length ?? 0}"); } catch { }
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
				DisplayName = State.PlayerTitle ?? (await _world.GetPlayerNameAsync(linked) ?? "RimAI.Common.Player".Translate().ToString()),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = true
			};
			State.Messages.Add(userMsg);
			// P14：立即写入命令类用户消息（不推进回合）
			try
			{
				var playerId = GetPlayerIdOrNull() ?? "player:unknown";
				await _history.AppendRecordAsync(State.ConvKey, "ChatUI", playerId, "chat", userText ?? string.Empty, advanceTurn: false, ct: linked).ConfigureAwait(false);
			}
			catch { }

			var aiMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.Ai,
				DisplayName = "RimAI.Common.Pawn".Translate().ToString(),
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
					// try { Verse.Log.Message($"[RimAI.Core][P12] Orchestration done ok={result != null}"); } catch { }

					// 显示一次过程说明（PlanTrace 首条）到 UI（历史写入已由编排完成）
					bool hasPlanTrace = false;
					if (result != null && result.PlanTrace != null && result.PlanTrace.Count > 0)
					{
						try
						{
							aiMsg.Text = result.PlanTrace[0] ?? string.Empty;
							hasPlanTrace = !string.IsNullOrWhiteSpace(aiMsg.Text);
						}
						catch { }
					}

					// 若存在 PlanTrace，则为“LLM 汇总”单独插入一条新的 AI 占位消息，确保 UI 显示为两行
					ChatMessage aiSummaryMsg = null;
					if (hasPlanTrace)
					{
						aiSummaryMsg = new ChatMessage
						{
							Id = Guid.NewGuid().ToString("N"),
							Sender = MessageSender.Ai,
							DisplayName = "RimAI.Common.Pawn".Translate().ToString(),
							TimestampUtc = DateTime.UtcNow,
							Text = string.Empty,
							IsCommand = true
						};
						State.Messages.Add(aiSummaryMsg);
					}

					// 构造 ExternalBlocks（RAG）注入工具结果概览
					var blocks = new List<ContextBlock>();
					if (result != null && result.Executions != null && result.Executions.Count > 0)
					{
						try
						{
							var title = string.IsNullOrWhiteSpace(result.HitDisplayName) ? "RimAI.ChatUI.Tools.ResultTitle".Translate().ToString() : ("RimAI.ChatUI.Tools.ResultTitleWithName".Translate(result.HitDisplayName).ToString());
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

					// 调试日志：RAG 块装载概览（用于观察工具结果是否正确注入）——放在 ExternalBlocks 注入与 Prompt.Build 之后

					// 段2：RAG→LLM 真流式
					var req2 = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = TryGetPawnLoadId(), IsCommand = true, Locale = null, UserInput = userText, ExternalBlocks = blocks };
					var prompt2 = await _prompting.BuildAsync(req2, linked).ConfigureAwait(false);
					SplitSpecialFromSystem(prompt2.SystemPrompt, out var systemFiltered3, out var specialLines3);
					var systemPayload2 = BuildSystemPayload(systemFiltered3, prompt2.ContextBlocks);
					// 调试日志：汇总阶段的最终 SystemPayload（含 ExternalBlocks 合并后的结果）
					try
					{
						var preview = systemPayload2 ?? string.Empty;
						if (preview.Length > 4000) preview = preview.Substring(0, 4000) + "...";
						Verse.Log.Message("[RimAI.Core][P12] Command Summary SystemPayload\nconv=" + State.ConvKey + "\n" + preview);
					}
					catch { }
					// 从发送给 LLM 的上下文中排除 PlanTrace，以避免模型复述“过程说明”；
					// 同时占位的“LLM 汇总”消息为空，将在 BuildMessagesArray 中被自动忽略。
					System.Collections.Generic.IReadOnlyList<ChatMessage> visibleForLlm = State.Messages;
					if (hasPlanTrace)
					{
						var tmp = new List<ChatMessage>(State.Messages.Count);
						foreach (var m in State.Messages) { if (!object.ReferenceEquals(m, aiMsg)) tmp.Add(m); }
						visibleForLlm = tmp;
					}
					var messages2 = BuildMessagesArray(systemPayload2, visibleForLlm);
					// 输出 Messages 列表（system+历史+本次用户输入）到日志
					LogMessagesList(State.ConvKey, messages2);
					var uiReq2 = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages2, Stream = true };
					await foreach (var r in _llm.StreamResponseAsync(uiReq2, linked))
					{
						if (!r.IsSuccess) { break; }
						// 仅在会话被新流替换时中断；用户取消通过专用按钮
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
					// 流完成后：在控制器侧统一合并残余分片并尝试写入最终历史（独立于 UI 是否在绘制）
					try { await TryFinalizeStreamingAndCommitAsync().ConfigureAwait(false); } catch { }
					// 流结束或提前中断时，确保 State.IsStreaming 复位
					State.IsStreaming = false;
				}
				catch (OperationCanceledException) { }
				catch (Exception) { }
				finally
				{
					try { _streamCts?.Dispose(); } catch { }
					_streamCts = null;
					State.IsStreaming = false;
					// 中断情况下，确保指示灯复位，避免 UI 假性“忙碌”
					State.Indicators.DataOn = false;
					State.Indicators.FinishOn = false;
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
			if (State.FinalCommittedThisTurn) return;
			var final = GetLastAiText();
			if (!string.IsNullOrEmpty(final))
			{
				var pawnId = TryGetPawnLoadId();
				string speaker = pawnId.HasValue ? ($"pawn:{pawnId.Value}") : GetFirstPawnIdOrNull() ?? "agent:stage";
				try { await _history.AppendRecordAsync(State.ConvKey, "ChatUI", speaker, "chat", final, advanceTurn: true).ConfigureAwait(false); State.FinalCommittedThisTurn = true; } catch { }
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

		// 统一在控制器侧将残余流式分片合并到最后一条 AI 消息，避免 UI 未渲染导致丢失
		private void AppendAllChunksToLastAiMessageInternal()
		{
			try
			{
				while (State.StreamingChunks.TryDequeue(out var c))
				{
					if (string.IsNullOrEmpty(c)) continue;
					for (var i = State.Messages.Count - 1; i >= 0; i--)
					{
						var m = State.Messages[i];
						if (m.Sender == MessageSender.Ai)
						{
							m.Text += c;
							break;
						}
					}
				}
			}
			catch { }
		}

		public async Task TryFinalizeStreamingAndCommitAsync()
		{
			try
			{
				AppendAllChunksToLastAiMessageInternal();
				await WriteFinalToHistoryIfAnyAsync().ConfigureAwait(false);
			}
			catch { }
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

		private string GetPlayerIdOrNull()
		{
			try
			{
				if (State?.ParticipantIds != null)
				{
					foreach (var id in State.ParticipantIds)
					{
						if (id != null && id.StartsWith("player:")) return id;
					}
				}
			}
			catch { }
			return null;
		}

		private string GetFirstPawnIdOrNull()
		{
			try
			{
				if (State?.ParticipantIds != null)
				{
					foreach (var id in State.ParticipantIds)
					{
						if (id != null && id.StartsWith("pawn:")) return id;
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
			return "RimAI.Common.Pawn".Translate().ToString();
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
					// 额外防护：过滤工具调用文案与明显非自然语言 JSON（避免模型误学）
					if (content.Length > 0 && content.Length < 8192 && content.TrimStart().StartsWith("[{") && content.TrimEnd().EndsWith("}]"))
					{
						continue;
					}
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
			// disabled in ChatUI
		}

		private static void LogOutboundRequest(string convKey, ChatMessage userMsg, ChatMessage aiMsg, PromptBuildResult prompt, string finalUserText, string mode)
		{
			// disabled in ChatUI
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


