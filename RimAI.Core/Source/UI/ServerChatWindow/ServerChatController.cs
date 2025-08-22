using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Prompting.Models;
using RimAI.Core.Source.Modules.Server;
using RimAI.Core.Source.UI.ServerChatWindow;
using Verse;
using RimAI.Core.Source.Modules.Orchestration;

namespace RimAI.Core.Source.UI.ServerChatWindow
{
	internal sealed class ServerChatController
	{
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;
		private readonly IPromptService _prompting;
		private readonly IServerService _server;
		private readonly IOrchestrationService _orchestration;

		private CancellationTokenSource _cts;

		public ServerChatConversationState State { get; }

		public ServerChatController(ILLMService llm, IHistoryService history, IPromptService prompting, IServerService server, IOrchestrationService orchestration, string convKey, IReadOnlyList<string> participantIds, string selectedServerEntityId)
		{
			_llm = llm ?? throw new ArgumentNullException(nameof(llm));
			_history = history ?? throw new ArgumentNullException(nameof(history));
			_prompting = prompting ?? throw new ArgumentNullException(nameof(prompting));
			_server = server ?? throw new ArgumentNullException(nameof(server));
			_orchestration = orchestration ?? throw new ArgumentNullException(nameof(orchestration));
			State = new ServerChatConversationState { ConvKey = convKey, ParticipantIds = participantIds, SelectedServerEntityId = selectedServerEntityId };
		}

		public async Task StartAsync()
		{
			try
			{
				await _history.UpsertParticipantsAsync(State.ConvKey, State.ParticipantIds).ConfigureAwait(false);
				var thread = await _history.GetThreadAsync(State.ConvKey, page: 1, pageSize: 200).ConfigureAwait(false);
				if (thread?.Entries != null)
				{
					foreach (var e in thread.Entries)
					{
						if (e == null || e.Deleted) continue;
						var msg = new ServerChatMessage
						{
							Id = e.Id ?? Guid.NewGuid().ToString("N"),
							Sender = e.Role == EntryRole.User ? ServerMessageSender.User : ServerMessageSender.Ai,
							DisplayName = e.Role == EntryRole.User ? (State.PlayerTitle ?? "RimAI.Common.Player".Translate().ToString()) : "RimAI.Core.Server.Chat.Ai".Translate().ToString(),
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

		public static (string convKey, List<string> participantIds) BuildConvForServer(string serverEntityId, string playerId)
		{
			var ids = new List<string>();
			if (!string.IsNullOrWhiteSpace(serverEntityId))
			{
				var eid = serverEntityId;
				if (!(eid.StartsWith("pawn:") || eid.StartsWith("thing:"))) eid = $"thing:{eid}";
				ids.Add(eid);
			}
			if (!string.IsNullOrWhiteSpace(playerId)) ids.Add(playerId);
			ids.Sort(StringComparer.Ordinal);
			var convKey = string.Join("|", ids);
			return (convKey, ids);
		}

		public void Cancel()
		{
			try { _cts?.Cancel(); } catch { }
			State.IsBusy = false;
		}

		public async Task SendSmalltalkAsync(string userText, CancellationToken ct = default)
		{
			Cancel();
			_cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var linked = _cts.Token;
			State.IsBusy = true;

			var userMsg = new ServerChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = ServerMessageSender.User,
				DisplayName = State.PlayerTitle ?? "RimAI.Common.Player".Translate().ToString(),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = false
			};
			State.Messages.Add(userMsg);
			try { await _history.AppendRecordAsync(State.ConvKey, "ServerChatUI", State.ParticipantIds?.FirstOrDefault(pid => pid.StartsWith("player:")) ?? "player:unknown", "chat", userText, advanceTurn: false, ct: linked).ConfigureAwait(false); } catch { }

			var aiMsg = new ServerChatMessage { Id = Guid.NewGuid().ToString("N"), Sender = ServerMessageSender.Ai, DisplayName = "RimAI.Core.Server.Chat.Ai".Translate().ToString(), TimestampUtc = DateTime.UtcNow, Text = string.Empty, IsCommand = false };
			State.Messages.Add(aiMsg);

			_ = Task.Run(async () =>
			{
				try
				{
					var locale = (string)null;
					var pack = await _server.BuildPromptAsync(State.SelectedServerEntityId, locale, linked).ConfigureAwait(false);
					var lines = pack?.SystemLines ?? Array.Empty<string>();
					var blocks = pack?.ContextBlocks ?? Array.Empty<ContextBlock>();
					var system = string.Join("\n", lines);
					var req = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = null, IsCommand = false, Locale = locale, UserInput = userText, ExternalBlocks = blocks.ToList() };
					var built = await _prompting.BuildAsync(req, linked).ConfigureAwait(false);
					var systemPayload = string.IsNullOrWhiteSpace(system) ? (built?.SystemPrompt ?? string.Empty) : (system + "\n" + (built?.SystemPrompt ?? string.Empty));
					var messages = new List<RimAI.Framework.Contracts.ChatMessage>();
					if (!string.IsNullOrWhiteSpace(systemPayload)) messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = systemPayload });
					foreach (var m in State.Messages)
					{
						var role = m.Sender == ServerMessageSender.User ? "user" : "assistant";
						if (!string.IsNullOrWhiteSpace(m.Text)) messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = role, Content = m.Text });
					}
					var uiReq = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages, Stream = false };
					var resp = await _llm.GetResponseAsync(uiReq, linked).ConfigureAwait(false);
					if (resp.IsSuccess)
					{
						aiMsg.Text = resp.Value?.Message?.Content ?? string.Empty;
						try { await _history.AppendRecordAsync(State.ConvKey, "ServerChatUI", State.SelectedServerEntityId ?? "agent:stage", "chat", aiMsg.Text, advanceTurn: true, ct: linked).ConfigureAwait(false); } catch { }
					}
				}
				catch (OperationCanceledException) { }
				catch (Exception) { }
				finally { State.IsBusy = false; try { _cts?.Dispose(); } catch { } _cts = null; }
			}, linked);
		}

		public async Task SendCommandAsync(string userText, CancellationToken ct = default)
		{
			Cancel();
			_cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var linked = _cts.Token;
			State.IsBusy = true;

			var userMsg = new ServerChatMessage
			{
				Id = Guid.NewGuid().ToString("N"),
				Sender = ServerMessageSender.User,
				DisplayName = State.PlayerTitle ?? "RimAI.Common.Player".Translate().ToString(),
				TimestampUtc = DateTime.UtcNow,
				Text = userText,
				IsCommand = true
			};
			State.Messages.Add(userMsg);
			try { await _history.AppendRecordAsync(State.ConvKey, "ServerChatUI", State.ParticipantIds?.FirstOrDefault(pid => pid.StartsWith("player:")) ?? "player:unknown", "chat", userText, advanceTurn: false, ct: linked).ConfigureAwait(false); } catch { }

			var aiMsg = new ServerChatMessage { Id = Guid.NewGuid().ToString("N"), Sender = ServerMessageSender.Ai, DisplayName = "RimAI.Core.Server.Chat.Ai".Translate().ToString(), TimestampUtc = DateTime.UtcNow, Text = string.Empty, IsCommand = true };
			State.Messages.Add(aiMsg);

			_ = Task.Run(async () =>
			{
				try
				{
					// 段1：编排（非流式）
					ToolCallsResult result = null;
					try
					{
						var options = new RimAI.Core.Source.Modules.Orchestration.ToolOrchestrationOptions { Mode = OrchestrationMode.Classic, Profile = ExecutionProfile.Fast, MaxCalls = 1 };
						result = await _orchestration.ExecuteAsync(userText, State.ParticipantIds, options, linked).ConfigureAwait(false);
					}
					catch { }

					// 构造 ExternalBlocks（RAG）注入工具结果概览
					var extBlocks = new List<ContextBlock>();
					if (result != null && result.Executions != null && result.Executions.Count > 0)
					{
						try
						{
							var compact = new List<object>();
							foreach (var e in result.Executions)
							{
								compact.Add(new { tool = e.ToolName, outcome = e.Outcome, result = e.ResultObject });
							}
							extBlocks.Add(new ContextBlock { Title = "工具执行结果", Text = Newtonsoft.Json.JsonConvert.SerializeObject(compact) });
						}
						catch { }
					}

					// 服务器提示词打包
					var locale = (string)null;
					var pack = await _server.BuildPromptAsync(State.SelectedServerEntityId, locale, linked).ConfigureAwait(false);
					var lines = pack?.SystemLines ?? Array.Empty<string>();
					var blocks = pack?.ContextBlocks ?? Array.Empty<ContextBlock>();
					var system = string.Join("\n", lines);
					// 合并外部块
					var mergedBlocks = new List<ContextBlock>();
					if (blocks != null) mergedBlocks.AddRange(blocks);
					if (extBlocks.Count > 0) mergedBlocks.AddRange(extBlocks);

					// 调用 P11 生成系统提示与用户前缀
					var req = new PromptBuildRequest { Scope = PromptScope.ChatUI, ConvKey = State.ConvKey, ParticipantIds = State.ParticipantIds, PawnLoadId = null, IsCommand = true, Locale = locale, UserInput = userText, ExternalBlocks = mergedBlocks };
					var built = await _prompting.BuildAsync(req, linked).ConfigureAwait(false);
					var systemPayload = string.IsNullOrWhiteSpace(system) ? (built?.SystemPrompt ?? string.Empty) : (system + "\n" + (built?.SystemPrompt ?? string.Empty));
					var messages = new List<RimAI.Framework.Contracts.ChatMessage>();
					if (!string.IsNullOrWhiteSpace(systemPayload)) messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = systemPayload });
					foreach (var m in State.Messages)
					{
						var role = m.Sender == ServerMessageSender.User ? "user" : "assistant";
						if (!string.IsNullOrWhiteSpace(m.Text)) messages.Add(new RimAI.Framework.Contracts.ChatMessage { Role = role, Content = m.Text });
					}
					var uiReq = new RimAI.Framework.Contracts.UnifiedChatRequest { ConversationId = State.ConvKey, Messages = messages, Stream = false };
					var resp = await _llm.GetResponseAsync(uiReq, linked).ConfigureAwait(false);
					if (resp.IsSuccess)
					{
						aiMsg.Text = resp.Value?.Message?.Content ?? string.Empty;
						try { await _history.AppendRecordAsync(State.ConvKey, "ServerChatUI", State.SelectedServerEntityId ?? "agent:stage", "chat", aiMsg.Text, advanceTurn: true, ct: linked).ConfigureAwait(false); } catch { }
					}
				}
				catch (OperationCanceledException) { }
				catch (Exception) { }
				finally { State.IsBusy = false; try { _cts?.Dispose(); } catch { } _cts = null; }
			}, linked);
		}
	}
}


