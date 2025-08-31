using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.History.Models;
using RimAI.Core.Source.Modules.History.Recap;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.UI.ChatWindow.Parts;
using RimAI.Core.Source.Modules.Prompting;
using RimAI.Core.Source.Modules.Persona;
using RimAI.Core.Source.Modules.Persona.Biography;
using RimAI.Core.Source.Modules.Persona.Ideology;
using RimAI.Core.Source.Infrastructure.Localization;
using RimAI.Core.Contracts.Config;

namespace RimAI.Core.Source.UI.ChatWindow
{
	public sealed class ChatWindow : Window
	{
		private readonly ServiceContainer _container;
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;
		private readonly IWorldDataService _world;
		private readonly IOrchestrationService _orchestration;
		private readonly IPromptService _prompting;
		private readonly IPersonaService _persona;
		private readonly IBiographyService _biography;
		private readonly IIdeologyService _ideology;
		private readonly IRecapService _recap;

		private Pawn _pawn;
		private ChatController _controller;
		private ChatTab _activeTab = ChatTab.History;
		private string _inputText = string.Empty;
		private string _titleInputText = string.Empty;
		private bool _titleInputInitialized;
		private Vector2 _scrollTranscript = Vector2.zero;
		private Vector2 _scrollRight = Vector2.zero;
		private Vector2 _scrollRoster = Vector2.zero;
        private bool _historyWritten;
		private bool _titleDirty;
		private static string _cachedPlayerId;
		private float _lastTranscriptContentHeight;
		private Texture _pawnPortrait;
		private Parts.HealthPulseState _healthPulse = new Parts.HealthPulseState();
		private float? _healthPercent;
		private bool _pawnDead;
		private CancellationTokenSource _healthCts;
		private string _lcdText;
		private System.Random _lcdRng;
		private double _lcdNextShuffleRealtime;

		public override Vector2 InitialSize => new Vector2(960f, 600f);

		private Parts.HistoryManagerTabView _historyView;
		private Parts.PersonaTabView _personaView;
		private Parts.FixedPromptTabView _fixedPromptView;
		private Parts.TitleSettingsTabView _titleSettingsView;

		public ChatWindow(Pawn pawn)
		{
			_pawn = pawn;
			ApplyWindowDefaults();

			_container = RimAICoreMod.Container;
			SyncLocaleDefaultIfNoOverride();
			var svc = ResolveServicesOnce();
			_llm = svc.llm;
			_history = svc.history;
			_world = svc.world;
			_orchestration = svc.orchestration;
			_prompting = svc.prompting;
			_persona = svc.persona;
			_biography = svc.biography;
			_ideology = svc.ideology;
			_recap = svc.recap;

			var participantIds = BuildParticipantIds(pawn);
			var convKey = BuildConvKey(participantIds);
			convKey = Parts.ConversationSwitching.TryReuseExistingConvKey(_history, participantIds, convKey);
			_controller = new ChatController(_llm, _history, _world, _orchestration, _prompting, convKey, participantIds);

			EnsurePlayerTitleInitialized();
			SubscribeConfigAndLocaleChanges();
			InitializeLcdRng(convKey);
			StartBackgroundTasks();
		}

		public override void PreOpen()
		{
			// 互斥：打开本窗口前，若服务器聊天窗口已开启，则将其关闭
			try { Verse.Find.WindowStack?.TryRemove(typeof(RimAI.Core.Source.UI.ServerChatWindow.ServerChatWindow), true); } catch { }
			base.PreOpen();
		}

		public override void DoWindowContents(Rect inRect)
		{
			// 若当前不在称谓设置页，允许下次进入时重新初始化输入框
			if (_activeTab != ChatTab.Title) _titleInputInitialized = false;
			// 固定提示页：保持原有逻辑，直接绘制并提前返回（不绘制左侧栏）
			if (_activeTab == ChatTab.FixedPrompt)
			{
				DrawFixedPromptTab(inRect);
				return;
			}

			// 合并后台初始化消息
			ConsumePendingInitMessages();

			// 计算布局
			var layout = ComputeLayout(inRect);

			// 左列
			EnsurePawnPortrait(96f);
			DrawLeftSidebar(layout.leftRect);

			// 右列按活动页分派
			if (_activeTab == ChatTab.Title)
			{
				if (_controller.State.IsStreaming) { _activeTab = ChatTab.History; }
				else { EnsureTitleTabStateInitialized(); DrawTitleSettingsTab(new Rect(layout.rightRectOuter.x, layout.rightRectOuter.y + 32f, layout.rightRectOuter.width, layout.rightRectOuter.height - 32f)); }
				return;
			}
			if (_activeTab == ChatTab.HistoryAdmin)
			{
				DrawHistoryManagerTab(new Rect(layout.rightRectOuter.x, layout.rightRectOuter.y + 32f, layout.rightRectOuter.width, layout.rightRectOuter.height - 32f));
				return;
			}
			if (_activeTab == ChatTab.Job)
			{
				Parts.JobManagerTab.Draw(new Rect(layout.rightRectOuter.x, layout.rightRectOuter.y + 32f, layout.rightRectOuter.width, layout.rightRectOuter.height - 32f), ref _scrollRight, p => OpenJobAssignDialog(p));
				return;
			}
			if (_activeTab == ChatTab.Persona)
			{
				DrawPersonaTab(layout.rightRectOuter, layout.titleRect);
				return;
			}
			if (_activeTab == ChatTab.Test)
			{
				Parts.TestTab.Draw(new Rect(layout.rightRectOuter.x, layout.rightRectOuter.y + 32f, layout.rightRectOuter.width, layout.rightRectOuter.height - 32f), _controller, _world, _container);
				return;
			}

			// 聊天主界面
			DrawChatMain(layout.titleRect, layout.transcriptRect, layout.indicatorRect, layout.inputRect, layout.rightRectOuter);
		}

		private void OnCancelStreaming()
		{
			_controller.CancelStreaming();
			// 若当前最后一条 AI 消息为空（半生成占位），直接删除，避免下次进入历史页混淆
			try
			{
				for (int i = _controller.State.Messages.Count - 1; i >= 0; i--)
				{
					var m = _controller.State.Messages[i];
					if (m.Sender == MessageSender.Ai && string.IsNullOrWhiteSpace(m.Text))
					{
						_controller.State.Messages.RemoveAt(i);
						break;
					}
				}
			}
			catch { }
			// 若控制器中有缓存的最后一次用户输入，则恢复到输入框
			if (!string.IsNullOrEmpty(_controller.State.LastUserInputStash))
			{
				_inputText = _controller.State.LastUserInputStash;
				_controller.State.LastUserInputStash = null;
			}
		}

		public override void PreClose()
		{
			base.PreClose();
			try { _healthCts?.Cancel(); } catch { }
		}

		private string BuildLcdText()
		{
			// 简易技术文案（不含敏感正文）：会话短ID与消息计数
			var idHash = _controller.State.ConvKey?.GetHashCode() ?? 0;
			int userCount = 0, aiCount = 0;
			foreach (var m in _controller.State.Messages)
				if (m.Sender == MessageSender.User) userCount++; else if (m.Sender == MessageSender.Ai) aiCount++;
			return $"RID:{(idHash & 0xFFFF):X4} U:{userCount} A:{aiCount}";
		}

		private async Task OnSendSmalltalkAsync()
		{
			var text = _inputText?.Trim();
			if (string.IsNullOrEmpty(text)) return;
			_inputText = string.Empty;
			_historyWritten = false; _controller.State.FinalCommittedThisTurn = false;
			await _controller.SendSmalltalkAsync(text);
		}

		private async Task OnSendCommandAsync()
		{
			var text = _inputText?.Trim();
			if (string.IsNullOrEmpty(text)) return;
			_inputText = string.Empty;
			_historyWritten = false; _controller.State.FinalCommittedThisTurn = false;
			await _controller.SendCommandAsync(text);
		}

		// DrawJobManagerTab 已移动到 Parts.JobManagerTab

		// 历史子页内部状态已迁移至 Parts.HistoryManagerTabView

		private void DrawHistoryManagerTab(Rect inRect)
		{
			// 重定向到 Parts.HistoryManagerTabView，保持主类精简
			if (_historyView == null) _historyView = new Parts.HistoryManagerTabView();
			_historyView.Draw(inRect, _controller.State, _history, _recap);
		}

		

		private void OpenJobAssignDialog(Verse.Pawn pawn)
		{
			var entityId = pawn != null ? ($"pawn:{pawn.thingIDNumber}") : null;
			var current = _persona.Get(entityId)?.Job;
			string name = current?.Name ?? string.Empty;
			string desc = current?.Description ?? string.Empty;
			Parts.JobManagerTab.OpenAssignDialog(entityId, name, desc, (n, d) => SetJob(pawn, n, d));
		}

		// JobAssignDialog 已移动到 Parts.JobManagerTab

		private string GetJobName(Verse.Pawn pawn)
		{
			try { var s = _persona.Get($"pawn:{pawn.thingIDNumber}")?.Job?.Name; return s ?? string.Empty; } catch { return string.Empty; }
		}
		private void SetJob(Verse.Pawn pawn, string name, string desc)
		{
			try
			{
				var entityId = $"pawn:{pawn.thingIDNumber}";
				var prev = _persona.Get(entityId)?.Job;
				var prevHas = prev != null && (!string.IsNullOrWhiteSpace(prev.Name) || !string.IsNullOrWhiteSpace(prev.Description));
				_persona.Upsert(entityId, e => e.SetJob(name, desc));
				// 写入历史（仅“最终输出”域，按规则写用户消息）
				string userText;
				if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc)) userText = "RimAI.ChatUI.Job.History.Removed".Translate();
				else if (prevHas) userText = "RimAI.ChatUI.Job.History.Transferred".Translate(name);
				else userText = "RimAI.ChatUI.Job.History.Assigned".Translate(name);
				// 历史键应基于被任命小人，而非当前窗体会话
				string playerId = null;
				try
				{
					if (_controller?.State?.ParticipantIds != null)
					{
						foreach (var id in _controller.State.ParticipantIds)
						{
							if (id != null && id.StartsWith("player:")) { playerId = id; break; }
						}
					}
				}
				catch { }
				if (string.IsNullOrEmpty(playerId)) playerId = GetOrCreatePlayerSessionId();
				var pids = new System.Collections.Generic.List<string> { entityId, playerId };
				var convKey = BuildConvKey(pids);
				convKey = Parts.ConversationSwitching.TryReuseExistingConvKey(_history, pids, convKey);
				_ = _history.AppendRecordAsync(convKey, "ChatUI", GetOrCreatePlayerSessionId(), "chat", userText, advanceTurn: false);
			}
			catch { }
		}

		private void BackToChatAndRefresh()
		{
			_activeTab = ChatTab.History;
			// 重置视图高度与滚动，使下一帧自动吸底并触发布局刷新
			_lastTranscriptContentHeight = 0f;
			_scrollTranscript = new Vector2(0f, float.MaxValue);
			// 清理历史页缓存，确保下次进入立即刷新
			try { _historyView?.ClearCache(); } catch { }
			try { _personaView?.ClearCache(); } catch { }
		}

		private void SwitchConversationToPawn(Verse.Pawn pawn)
		{
			if (pawn == null) return;
			try
			{
				// 切换当前小人并重置相关缓存
				_pawn = pawn;
				_pawnPortrait = null;
				_healthPercent = null;
				_pawnDead = false;

				// 重建 participantIds：目标 pawn + 已有 playerId（若无则现取）
				var participantIds = new System.Collections.Generic.List<string>();
				participantIds.Add($"pawn:{pawn.thingIDNumber}");
				string playerId = null;
				try
				{
					if (_controller?.State?.ParticipantIds != null)
					{
						foreach (var id in _controller.State.ParticipantIds)
						{
							if (id != null && id.StartsWith("player:")) { playerId = id; break; }
						}
					}
				}
				catch { }
				if (string.IsNullOrEmpty(playerId)) playerId = GetOrCreatePlayerSessionId();
				participantIds.Add(playerId);
				participantIds.Sort(System.StringComparer.Ordinal);
				var convKey = BuildConvKey(participantIds);
				convKey = Parts.ConversationSwitching.TryReuseExistingConvKey(_history, participantIds, convKey);
				_controller = new ChatController(_llm, _history, _world, _orchestration, _prompting, convKey, participantIds);
				// 清空并重新加载该对话的历史
				_controller.State.Messages.Clear();
				_historyWritten = false;
				_ = _controller.StartAsync();
				// 切回聊天并刷新
				BackToChatAndRefresh();
			}
			catch { }
		}

		private void DrawFixedPromptTab(Rect inRect)
		{
			if (_fixedPromptView == null) _fixedPromptView = new Parts.FixedPromptTabView();
			_fixedPromptView.Draw(inRect, _pawn, _persona, () => { _activeTab = ChatTab.History; });
		}

		private void DrawTitleSettingsTab(Rect inRect)
		{
			if (_titleSettingsView == null) _titleSettingsView = new Parts.TitleSettingsTabView();
			var loc = _container.Resolve<ILocalizationService>();
			var cfg = _container.Resolve<IConfigurationService>();
			_titleSettingsView.Draw(inRect, loc, cfg, _ => { try { RefreshPlayerTitle(); } catch { } });
		}

		private void AppendToLastAiMessage(string delta)
		{
			for (var i = _controller.State.Messages.Count - 1; i >= 0; i--)
			{
				var m = _controller.State.Messages[i];
				if (m.Sender == MessageSender.Ai)
				{
					m.Text += delta;
					break;
				}
			}
		}

		private static List<string> BuildParticipantIds(Pawn pawn)
		{
			var list = new List<string>();
			if (pawn != null && pawn.thingIDNumber != 0) list.Add($"pawn:{pawn.thingIDNumber}");
			var playerId = GetOrCreatePlayerSessionId();
			list.Add(playerId);
			list.Sort(StringComparer.Ordinal);
			return list;
		}

        private void AppendAllChunksToLastAiMessage()
        {
            while (_controller.TryDequeueChunk(out var c))
            {
                AppendToLastAiMessage(c);
            }
        }

		private static string BuildConvKey(IReadOnlyList<string> participantIds)
		{
			return string.Join("|", participantIds);
		}

		// TryBindExistingConversation 已移动到 Parts.ConversationSwitching

		private static string GetJobTitleOrNone(Pawn pawn)
		{
			try
			{
				var title = RimAI.Core.Source.Versioned._1_6.World.WorldApiV16.GetPawnTitle(pawn);
				return string.IsNullOrWhiteSpace(title) ? "" : title;
			}
			catch { return ""; }
		}

		private static float ComputeTranscriptViewHeight(Rect rect, ChatConversationState state)
		{
			// 与 ChatTranscriptView 使用相同的度量逻辑，确保一致
			var contentW = rect.width - 16f;
			var textW = contentW - 12f;
			float totalHeight = 0f;
			for (int i = 0; i < state.Messages.Count; i++)
			{
				var m = state.Messages[i];
				var label = $"[{m.DisplayName} {m.TimestampUtc.ToLocalTime():HH:mm:ss}] {m.Text}";
				var textH = Mathf.Max(24f, Text.CalcHeight(label, textW));
				totalHeight += textH + 6f;
			}
			return Math.Max(rect.height, totalHeight + 8f);
		}

		private static string GetOrCreatePlayerSessionId()
		{
			if (!string.IsNullOrEmpty(_cachedPlayerId)) return _cachedPlayerId;
			_cachedPlayerId = $"player:{Guid.NewGuid().ToString("N").Substring(0, 8)}";
			return _cachedPlayerId;
		}

		private void EnsurePawnPortrait(float size)
		{
			if (_pawn == null) { _pawnPortrait = null; return; }
			if (_pawnPortrait == null)
			{
				// try { Verse.Log.Message($"[RimAI.Core][P10] Build pawn portrait begin id={_pawn?.thingIDNumber ?? 0}"); } catch { }
				var sz = new Vector2(size, size);
				_pawnPortrait = PortraitsCache.Get(_pawn, sz, Rot4.South);
				// try { Verse.Log.Message("[RimAI.Core][P10] Build pawn portrait done"); } catch { }
			}
		}

		private void RefreshPlayerTitle()
		{
			try
			{
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var loc = _container.Resolve<ILocalizationService>();
				var locale = cfg?.GetInternal()?.General?.Locale ?? "en";
				var title = cfg?.GetPlayerTitleOrDefault();
				if (string.IsNullOrWhiteSpace(title))
				{
					title = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
				}
				_controller.State.PlayerTitle = title;
			}
			catch { }
		}

		private async System.Threading.Tasks.Task PollHealthAsync(System.Threading.CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					if (_pawn != null && _pawn.thingIDNumber != 0 && _world != null)
					{
						var snap = await _world.GetPawnHealthSnapshotAsync(_pawn.thingIDNumber, ct);
						_pawnDead = snap.IsDead;
						// 平均值计算在 Tool 层；此处按需求直接计算用于 UI 展示
						_healthPercent = (snap.Consciousness + snap.Moving + snap.Manipulation + snap.Sight + snap.Hearing + snap.Talking + snap.Breathing + snap.BloodPumping + snap.BloodFiltration + snap.Metabolism) / 10f * 100f;
					}
				}
				catch { }
				await System.Threading.Tasks.Task.Delay(3000, ct);
			}
		}

		private string GetOrShuffleLcdText()
		{
			// 每 N 秒刷新一次滚动文案，随机队列拼接（稳定的 RNG 种子，确保不同会话一致）
			var now = Time.realtimeSinceStartup;
			if (now >= _lcdNextShuffleRealtime || string.IsNullOrEmpty(_lcdText))
			{
				var parts = new[]
				{
					"RIMAI", "CORE", "V5", "SAFE", "FAST", "STABLE", "AGENT", "STAGE", "TOOL", "WORLD",
					"HISTORY", "P3", "P4", "P5", "P6", "P7", "P8", "P9", "P10", "AI"
				};
				// 洗牌
				for (int i = parts.Length - 1; i > 0; i--)
				{
					int j = _lcdRng.Next(i + 1);
					var tmp = parts[i];
					parts[i] = parts[j];
					parts[j] = tmp;
				}
				// 取前若干并拼接，间隔更紧凑（单空格），并尽量等一轮跑完后再刷新
				var take = Mathf.Clamp(8, 3, parts.Length);
				var sel = string.Join(" ", parts, 0, take);
				_lcdText = sel + " ";
				// 估算一轮滚动耗时：根据列数和 Marquee 步进间隔，直到完整循环后再刷新
				Parts.LcdMarquee.EnsureColumns(_controller.State.Lcd, _lcdText);
				int totalCols = Mathf.Max(1, _controller.State.Lcd.Columns.Count);
				// 每次推进 3 列；每步间隔由 state.IntervalSec 指定
				float secsPerLoop = (totalCols / 3f) * (float)_controller.State.Lcd.IntervalSec;
				_lcdNextShuffleRealtime = now + Mathf.Max(4f, secsPerLoop);
			}
			return _lcdText;
		}

		// ---------- helpers (no behavior change) ----------
		private void ApplyWindowDefaults()
		{
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
			absorbInputAroundWindow = false; // 允许点击窗体外的游戏控件
			closeOnClickedOutside = false;
			closeOnAccept = false; // Enter 不关闭
			closeOnCancel = true;  // ESC 关闭
		}

		private void SyncLocaleDefaultIfNoOverride()
		{
			try
			{
				var locAuto = _container.Resolve<ILocalizationService>();
				var cfgAuto = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var gameLang = LanguageDatabase.activeLanguage?.folderName ?? "English";
				if (string.IsNullOrWhiteSpace(cfgAuto?.GetPromptLocaleOverrideOrNull()))
				{
					locAuto?.SetDefaultLocale(gameLang);
				}
			}
			catch { }
		}

		private (ILLMService llm, IHistoryService history, IWorldDataService world, IOrchestrationService orchestration, IPromptService prompting, IPersonaService persona, IBiographyService biography, IIdeologyService ideology, IRecapService recap) ResolveServicesOnce()
		{
			return (
				_container.Resolve<ILLMService>(),
				_container.Resolve<IHistoryService>(),
				_container.Resolve<IWorldDataService>(),
				_container.Resolve<IOrchestrationService>(),
				_container.Resolve<IPromptService>(),
				_container.Resolve<IPersonaService>(),
				_container.Resolve<IBiographyService>(),
				_container.Resolve<IIdeologyService>(),
				_container.Resolve<IRecapService>()
			);
		}

		private void EnsurePlayerTitleInitialized()
		{
			try
			{
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var loc = _container.Resolve<ILocalizationService>();
				var locale = cfg?.GetInternal()?.General?.Locale ?? "en";
				var title = cfg?.GetPlayerTitleOrDefault();
				if (string.IsNullOrWhiteSpace(title))
				{
					title = loc?.Get(locale, "ui.chat.player_title.value", loc?.Get("en", "ui.chat.player_title.value", "governor") ?? "governor") ?? "governor";
				}
				_controller.State.PlayerTitle = title;
				try { if (string.IsNullOrWhiteSpace(cfg?.GetPlayerTitleOrDefault())) cfg?.SetPlayerTitle(title); } catch { }
			}
			catch { }
		}

		private void SubscribeConfigAndLocaleChanges()
		{
			try
			{
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				var loc = _container.Resolve<ILocalizationService>();
				if (cfg != null) cfg.OnConfigurationChanged += _ => { try { _titleDirty = true; } catch { } };
				if (loc != null) loc.OnLocaleChanged += _ => { try { _titleDirty = true; } catch { } };
			}
			catch { }
		}

		private void InitializeLcdRng(string convKey)
		{
			_lcdRng = new System.Random((convKey ?? string.Empty).GetHashCode());
			_lcdNextShuffleRealtime = 0.0;
		}

		private void StartBackgroundTasks()
		{
			_healthCts = new CancellationTokenSource();
			_ = PollHealthAsync(_healthCts.Token);
			_ = _controller.StartAsync();
		}

		// ----- DoWindowContents helpers (behavior-preserving) -----
		private void ConsumePendingInitMessages()
		{
			while (_controller.State.PendingInitMessages.TryDequeue(out var initMsg))
			{
				_controller.State.Messages.Add(initMsg);
			}
		}

		private (Rect leftRect, Rect rightRectOuter, Rect titleRect, Rect transcriptRect, Rect indicatorRect, Rect inputRect) ComputeLayout(Rect inRect)
		{
			var leftW = inRect.width * (1f / 6f) + 55f;
			var rightW = inRect.width - leftW - 8f;
			var leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
			var rightRectOuter = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);
			var titleH = 70f;
			var indicatorH = 20f;
			var inputH = 60f;
			var titleRect = new Rect(rightRectOuter.x, rightRectOuter.y, rightRectOuter.width, titleH);
			var transcriptRect = new Rect(rightRectOuter.x, titleRect.yMax + 4f, rightRectOuter.width, rightRectOuter.height - titleH - indicatorH - inputH - 26f);
			var indicatorRect = new Rect(rightRectOuter.x, transcriptRect.yMax + 4f, rightRectOuter.width, indicatorH);
			var inputRect = new Rect(rightRectOuter.x, indicatorRect.yMax + 12f, rightRectOuter.width, inputH);
			return (leftRect, rightRectOuter, titleRect, transcriptRect, indicatorRect, inputRect);
		}

		private void DrawLeftSidebar(Rect leftRect)
		{
			LeftSidebarCard.Draw(
				leftRect,
				ref _activeTab,
				_pawnPortrait as Texture2D,
				_pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(),
				GetJobTitleOrNone(_pawn),
				ref _scrollRoster,
				onBackToChat: () => { try { if (_pawn != null) SwitchConversationToPawn(_pawn); } catch { } BackToChatAndRefresh(); _titleDirty = true; },
				onSelectPawn: p => SwitchConversationToPawn(p),
				getJobTitle: GetJobName,
				isStreaming: _controller.State.IsStreaming
			);
		}

		private void EnsureTitleTabStateInitialized()
		{
			try
			{
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
				_controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault();
				if (!_titleInputInitialized)
				{
					_titleInputText = _controller.State.PlayerTitle ?? "governor";
					_titleInputInitialized = true;
				}
			}
			catch { }
		}

		private void DrawPersonaTab(Rect rightRectOuter, Rect titleRect)
		{
			Parts.ConversationHeader.Draw(titleRect, _pawnPortrait, _pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(), GetJobName(_pawn), _healthPulse, _healthPercent, _pawnDead);
			if (_personaView == null) _personaView = new Parts.PersonaTabView();
			string entityId = _pawn != null && _pawn.thingIDNumber != 0 ? ($"pawn:{_pawn.thingIDNumber}") : null;
			var titleH = 70f;
			var bodyRect = new Rect(rightRectOuter.x, titleRect.yMax + 4f, rightRectOuter.width, rightRectOuter.height - titleH - 8f);
			_personaView.Draw(bodyRect, entityId, _controller.State.ConvKey, _persona, _biography, _ideology);
		}

		private void DrawChatMain(Rect titleRect, Rect transcriptRect, Rect indicatorRect, Rect inputRect, Rect rightRectOuter)
		{
			if (_titleDirty) { try { RefreshPlayerTitle(); } catch { } _titleDirty = false; }
			Parts.ConversationHeader.Draw(titleRect, _pawnPortrait, _pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(), GetJobName(_pawn), _healthPulse, _healthPercent, _pawnDead);

			var prevViewH = _lastTranscriptContentHeight;
			var newViewH = ComputeTranscriptViewHeight(transcriptRect, _controller.State);
			var prevMaxScrollY = Mathf.Max(0f, prevViewH - transcriptRect.height);
			bool wasNearBottom = _scrollTranscript.y >= (prevMaxScrollY - 20f);

			ChatTranscriptView.Draw(transcriptRect, _controller.State, _scrollTranscript, out _scrollTranscript);

			if (wasNearBottom && newViewH > prevViewH + 1f)
			{
				_scrollTranscript.y = newViewH;
			}
			_lastTranscriptContentHeight = newViewH;

			IndicatorLights.Draw(indicatorRect, _controller.State.Indicators, _controller.State.IsStreaming);

			var lcdLeft = indicatorRect.x + 180f;
			if (lcdLeft < indicatorRect.xMax)
			{
				const float lcdRightMargin = 5f;
				var lcdRect = new Rect(lcdLeft, indicatorRect.y, Mathf.Max(0f, indicatorRect.xMax - lcdLeft - lcdRightMargin), indicatorRect.height);
				var pulse = _controller.State.Indicators.DataOn;
				var text = GetOrShuffleLcdText();
				Parts.LcdMarquee.Draw(lcdRect, _controller.State.Lcd, text, pulse, _controller.State.IsStreaming);
			}

			InputRow.Draw(
				inputRect,
				ref _inputText,
				onSmalltalk: () => _ = OnSendSmalltalkAsync(),
				onCommand: () => _ = OnSendCommandAsync(),
				onCancel: () => OnCancelStreaming(),
				isStreaming: _controller.State.IsStreaming || _pawnDead
			);

			if (_controller.TryDequeueChunk(out var chunk))
			{
				AppendToLastAiMessage(chunk);
				// 流式分片追加后，强制滚动到底部以展示最新内容
				var forcedH = ComputeTranscriptViewHeight(transcriptRect, _controller.State);
				_scrollTranscript.y = forcedH;
				_lastTranscriptContentHeight = forcedH;
			}

			if (DateTime.UtcNow > _controller.State.Indicators.DataBlinkUntilUtc)
			{
				_controller.State.Indicators.DataOn = false;
			}

			if (_controller.State.Indicators.FinishOn && !_historyWritten)
			{
				try { AppendAllChunksToLastAiMessage(); _historyWritten = true; } catch { }
				// 结束时合并残余分片后，同样吸底
				var forcedH2 = ComputeTranscriptViewHeight(transcriptRect, _controller.State);
				_scrollTranscript.y = forcedH2;
				_lastTranscriptContentHeight = forcedH2;
			}
		}
	}
}


