using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
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
							DisplayName = e.Role == EntryRole.User ? playerName : "Pawn",
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
			State.IsStreaming = true;
            // 新会话开始：复位 Finish 指示灯，并推进流式会话编号，屏蔽旧流的延迟包
            State.Indicators.FinishOn = false;
            unchecked { State.ActiveStreamId++; }
            int currentStreamId = State.ActiveStreamId;

			var userMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.User,
				DisplayName = await _world.GetPlayerNameAsync(linked) ?? "Player",
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = false
			};
			State.Messages.Add(userMsg);

			var aiMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.Ai,
				DisplayName = "Pawn",
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
					var systemPrompt = prompt.SystemPrompt;
					var finalUserText = ComposeUserMessage(prompt);
					await foreach (var r in _llm.StreamResponseAsync(State.ConvKey,
						systemPrompt,
						finalUserText,
						linked))
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
			State.IsStreaming = true;
            // 新会话开始：复位 Finish 指示灯，并推进流式会话编号，屏蔽旧流的延迟包
            State.Indicators.FinishOn = false;
            unchecked { State.ActiveStreamId++; }
            int currentStreamId = State.ActiveStreamId;

			var userMsg = new ChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = MessageSender.User,
				DisplayName = await _world.GetPlayerNameAsync(linked) ?? "Player",
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
					var req = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = TryGetPawnLoadId(), IsCommand = true, Locale = null, UserInput = userText };
					var prompt = await _prompting.BuildAsync(req, linked).ConfigureAwait(false);
					var finalUserText = ComposeUserMessage(prompt);
					var result = await _orchestration.ExecuteAsync(finalUserText, State.ParticipantIds, new RimAI.Core.Source.Modules.Orchestration.ToolOrchestrationOptions
					{
						Mode = RimAI.Core.Source.Modules.Orchestration.OrchestrationMode.Classic,
						Profile = RimAI.Core.Source.Modules.Orchestration.ExecutionProfile.Fast,
						MaxCalls = 1
					}, linked);
					var text = ExtractTextFromOrchestrationResult(result);
					foreach (var piece in SliceForPseudoStream(text, 36))
					{
						linked.ThrowIfCancellationRequested();
						if (currentStreamId != State.ActiveStreamId) break;
						State.StreamingChunks.Enqueue(piece);
						var nowTick = DateTime.UtcNow;
						if (nowTick >= State.Indicators.DataNextAllowedBlinkUtc)
						{
							State.Indicators.DataOn = true;
							double factor = 0.7 + _rng.Value.NextDouble() * 0.6; // 0.7..1.3
							State.Indicators.DataBlinkUntilUtc = nowTick.AddMilliseconds(120 * factor);
							State.Indicators.DataNextAllowedBlinkUtc = nowTick.AddMilliseconds(160 * factor);
						}
						await Task.Delay(40, linked);
					}
					State.Indicators.FinishOn = true;
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

		public async void CancelStreaming()
		{
			try { _streamCts?.Cancel(); } catch { }
			// 若仍在流式，删除最后一次用户发言及半生成的 AI，并把文本归还输入框；清空剩余 chunk
			if (State.IsStreaming)
			{
				// 1) 使旧流失效
				unchecked { State.ActiveStreamId++; }
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
				// 5) 失效 Framework 会话缓存（防止上游缓存导致下一次响应延迟）
				try { await _llm.InvalidateConversationCacheAsync(State.ConvKey); } catch { }
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

		private static string ComposeUserMessage(PromptBuildResult prompt)
		{
			if (prompt == null) return string.Empty;
			var sb = new System.Text.StringBuilder();
			if (prompt.ContextBlocks != null)
			{
				foreach (var b in prompt.ContextBlocks)
				{
					if (!string.IsNullOrWhiteSpace(b.Title)) sb.AppendLine(b.Title);
					if (!string.IsNullOrWhiteSpace(b.Text)) sb.AppendLine(b.Text);
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
	}
}


