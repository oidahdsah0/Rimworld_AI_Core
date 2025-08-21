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
using RimAI.Core.Source.Modules.Persistence;
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

		public ChatWindow(Pawn pawn)
		{
			_pawn = pawn;
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
			absorbInputAroundWindow = false; // 允许点击窗体外的游戏控件
			closeOnClickedOutside = false;
			// 确保 Enter/Escape 不会触发默认 Close 行为，由我们自行处理
			closeOnAccept = false;
			closeOnCancel = true; // 允许 ESC 关闭 ChatUI

			_container = RimAICoreMod.Container;
			_llm = _container.Resolve<ILLMService>();
			_history = _container.Resolve<IHistoryService>();
			_world = _container.Resolve<IWorldDataService>();
			_orchestration = _container.Resolve<IOrchestrationService>();
			_prompting = _container.Resolve<IPromptService>();
			_persona = _container.Resolve<IPersonaService>();
			_biography = _container.Resolve<IBiographyService>();
			_ideology = _container.Resolve<IIdeologyService>();
			_recap = _container.Resolve<IRecapService>();

			var participantIds = BuildParticipantIds(pawn);
			var convKey = BuildConvKey(participantIds);
			// 若已存在与该 pawn 相关的历史会话，优先复用其 convKey（避免因 playerId 不稳定导致历史无法匹配）
			convKey = Parts.ConversationSwitching.TryReuseExistingConvKey(_history, participantIds, convKey);
			_controller = new ChatController(_llm, _history, _world, _orchestration, _prompting, convKey, participantIds);
			// 初始化时加载玩家称谓：优先设置值，其次按当前语言的本地化默认值
			try 
			{ 
				var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; 
				var loc = _container.Resolve<ILocalizationService>();
				var locale = cfg?.GetInternal()?.General?.Locale ?? "zh-Hans";
				var title = cfg?.GetPlayerTitleOrDefault() ?? string.Empty;
				if (string.IsNullOrWhiteSpace(title) || (title == "总督" && !locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase)))
				{
					title = loc?.Get(locale, "ui.chat.player_title.value", "总督") ?? "总督";
				}
				_controller.State.PlayerTitle = title;
			} catch { }
			// 异步从持久化文件加载覆盖值，然后更新配置与当前会话显示名
			_ = System.Threading.Tasks.Task.Run(async () =>
			{
				try
				{
					var persistence = _container.Resolve<IPersistenceService>();
					var json = await persistence.ReadTextUnderConfigOrNullAsync("UI/ChatWindow/player_title.json");
					if (!string.IsNullOrWhiteSpace(json))
					{
						var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, string>>(json) 
							?? new System.Collections.Generic.Dictionary<string, string>();
						if (obj.TryGetValue("player_title", out var persisted) && !string.IsNullOrWhiteSpace(persisted))
						{
							var cfg2 = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
							cfg2.SetPlayerTitle(persisted.Trim());
							_controller.State.PlayerTitle = cfg2.GetPlayerTitleOrDefault();
						}
					}
				}
				catch { }
			});
			_lcdRng = new System.Random(convKey.GetHashCode());
			_lcdNextShuffleRealtime = 0.0;
			// 启动健康轮询
			_healthCts = new CancellationTokenSource();
			_ = PollHealthAsync(_healthCts.Token);
			// 加载历史（若无则空）
			_ = _controller.StartAsync();
		}

		public override void DoWindowContents(Rect inRect)
		{
			// 若当前不在称谓设置页，允许下次进入时重新初始化输入框
			if (_activeTab != ChatTab.Title) _titleInputInitialized = false;
			if (_activeTab == ChatTab.FixedPrompt)
			{
				DrawFixedPromptTab(inRect);
				return;
			}
			// 历史页绘制延后到布局计算之后
			// 将后台初始化加载的历史消息在主线程合并到可见消息列表
			while (_controller.State.PendingInitMessages.TryDequeue(out var initMsg))
			{
				_controller.State.Messages.Add(initMsg);
			}
			var leftW = inRect.width * (1f / 6f) + 45f; // 人员名单放宽 30px
			var rightW = inRect.width - leftW - 8f;
			var leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
			var rightRectOuter = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);
			var titleH = 72f; // 放大标题栏用于半身像展示
			var indicatorH = 20f;
			var inputH = 60f; // 输入栏高度与按钮同高
			var titleRect = new Rect(rightRectOuter.x, rightRectOuter.y, rightRectOuter.width, titleH);
			var transcriptRect = new Rect(rightRectOuter.x, titleRect.yMax + 4f, rightRectOuter.width, rightRectOuter.height - titleH - indicatorH - inputH - 26f);
			var indicatorRect = new Rect(rightRectOuter.x, transcriptRect.yMax + 4f, rightRectOuter.width, indicatorH);
			var inputRect = new Rect(rightRectOuter.x, indicatorRect.yMax + 12f, rightRectOuter.width, inputH);

			// 左列
			EnsurePawnPortrait(96f);
			LeftSidebarCard.Draw(leftRect, ref _activeTab, _pawnPortrait as Texture2D, _pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(), GetJobTitleOrNone(_pawn), ref _scrollRoster, onBackToChat: () => { try { if (_pawn != null) SwitchConversationToPawn(_pawn); } catch { } BackToChatAndRefresh(); }, onSelectPawn: p => SwitchConversationToPawn(p), getJobTitle: GetJobName);
			// 若切换到历史页或聊天主界面，也刷新一次称谓，确保前缀/抬头正确
			if (_activeTab == ChatTab.History)
			{
				try { var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; _controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault(); } catch { }
			}
			if (_activeTab == ChatTab.FixedPrompt)
			{
				DrawFixedPromptTab(new Rect(rightRectOuter.x, rightRectOuter.y + 32f, rightRectOuter.width, rightRectOuter.height - 32f));
				return;
			}
			if (_activeTab == ChatTab.Title)
			{
				// 进入称谓设置页时刷新一次 PlayerTitle；输入框仅在首次进入时初始化，编辑期间不被覆盖
				try 
				{ 
					var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; 
					_controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault();
					if (!_titleInputInitialized)
					{
						_titleInputText = _controller.State.PlayerTitle ?? "总督";
						_titleInputInitialized = true;
					}
				} catch { }
				DrawTitleSettingsTab(new Rect(rightRectOuter.x, rightRectOuter.y + 32f, rightRectOuter.width, rightRectOuter.height - 32f));
				return;
			}
			if (_activeTab == ChatTab.HistoryAdmin)
			{
				DrawHistoryManagerTab(new Rect(rightRectOuter.x, rightRectOuter.y + 32f, rightRectOuter.width, rightRectOuter.height - 32f));
				return;
			}
			if (_activeTab == ChatTab.Job)
			{
				Parts.JobManagerTab.Draw(new Rect(rightRectOuter.x, rightRectOuter.y + 32f, rightRectOuter.width, rightRectOuter.height - 32f), ref _scrollRight, p => OpenJobAssignDialog(p));
				return;
			}
			if (_activeTab == ChatTab.Persona)
			{
				// 标题栏（同聊天主界面）
				try { var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; _controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault(); } catch { }
				Parts.ConversationHeader.Draw(titleRect, _pawnPortrait, _pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(), GetJobName(_pawn), _healthPulse, _healthPercent, _pawnDead);
				// 内容区域：人格信息子Tab（带 Biography/Ideology 两个子页）
				if (_personaView == null) _personaView = new Parts.PersonaTabView();
				string entityId = _pawn != null && _pawn.thingIDNumber != 0 ? ($"pawn:{_pawn.thingIDNumber}") : null;
				var bodyRect = new Rect(rightRectOuter.x, titleRect.yMax + 4f, rightRectOuter.width, rightRectOuter.height - titleH - 8f);
				_personaView.Draw(bodyRect, entityId, _controller.State.ConvKey, _persona, _biography, _ideology);
				return;
			}

			// 右列：标题 + 生命体征
			// 在绘制标题前刷新称谓（避免 UI 切页未触发时的滞后）
			try { var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService; _controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault(); } catch { }
			Parts.ConversationHeader.Draw(titleRect, _pawnPortrait, _pawn?.LabelCap ?? "RimAI.Common.Pawn".Translate(), GetJobName(_pawn), _healthPulse, _healthPercent, _pawnDead);

			// 在绘制前计算内容高度并判断是否需要自动吸底
			var prevViewH = _lastTranscriptContentHeight;
			var newViewH = ComputeTranscriptViewHeight(transcriptRect, _controller.State);
			var prevMaxScrollY = Mathf.Max(0f, prevViewH - transcriptRect.height);
			bool wasNearBottom = _scrollTranscript.y >= (prevMaxScrollY - 20f);

			ChatTranscriptView.Draw(transcriptRect, _controller.State, _scrollTranscript, out _scrollTranscript);

			// 若先前处于底部附近且内容增长，则自动滚动到底（不打断用户向上查看）
			if (wasNearBottom && newViewH > prevViewH + 1f)
			{
				_scrollTranscript.y = newViewH; // 设置为大值，Unity 会夹取为最大滚动位置
			}
			_lastTranscriptContentHeight = newViewH;
			// 左侧指示灯（Busy=黄色：根据 IsStreaming）
			IndicatorLights.Draw(indicatorRect, _controller.State.Indicators, _controller.State.IsStreaming);
			// 右侧剩余区域绘制 LCD 跑马灯
			var lcdLeft = indicatorRect.x + 180f; // 预留左侧指示灯区域宽度
			if (lcdLeft < indicatorRect.xMax)
			{
				const float lcdRightMargin = 5f; // 右端向左缩 5px
				var lcdRect = new Rect(lcdLeft, indicatorRect.y, Mathf.Max(0f, indicatorRect.xMax - lcdLeft - lcdRightMargin), indicatorRect.height);
				var pulse = _controller.State.Indicators.DataOn;
				var text = GetOrShuffleLcdText();
				Parts.LcdMarquee.Draw(lcdRect, _controller.State.Lcd, text, pulse, _controller.State.IsStreaming);
			}
			InputRow.Draw(inputRect, ref _inputText,
				onSmalltalk: () => _ = OnSendSmalltalkAsync(),
				onCommand: () => _ = OnSendCommandAsync(),
				onCancel: () => OnCancelStreaming(),
				isStreaming: _controller.State.IsStreaming || _pawnDead);

			// 若用户刚刚发送了消息，切换到历史页时应立即看到更新：当活跃页为 HistoryAdmin 时强制刷新
			if (_activeTab == ChatTab.HistoryAdmin && _historyView != null)
			{
				try { _historyView.ForceReloadHistory(_history, _controller.State.ConvKey); _historyView.ForceReloadRecaps(_recap, _controller.State.ConvKey); } catch { }
			}

			// 消费流式 chunk：将其累加到最后一条 AI 文本
			if (_controller.TryDequeueChunk(out var chunk))
			{
				AppendToLastAiMessage(chunk);
			}

			// 更新 Data 灯熄灭时机
			if (DateTime.UtcNow > _controller.State.Indicators.DataBlinkUntilUtc)
			{
				_controller.State.Indicators.DataOn = false;
			}

            // 流式完成后写入历史（仅一次）
            if (_controller.State.Indicators.FinishOn && !_historyWritten)
            {
                AppendAllChunksToLastAiMessage();
                _ = _controller.WriteFinalToHistoryIfAnyAsync();
                _historyWritten = true;
            }
		}

		private void OnCancelStreaming()
		{
			_controller.CancelStreaming();
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
			_historyWritten = false;
			await _controller.SendSmalltalkAsync(text);
		}

		private async Task OnSendCommandAsync()
		{
			var text = _inputText?.Trim();
			if (string.IsNullOrEmpty(text)) return;
			_inputText = string.Empty;
			_historyWritten = false;
			await _controller.SendCommandAsync(text);
		}

		// DrawJobManagerTab 已移动到 Parts.JobManagerTab

		// 历史子页内部状态已迁移至 Parts.HistoryManagerTabView

		private void DrawHistoryManagerTab(Rect inRect)
		{
			// 重定向到 Parts.HistoryManagerTabView，保持主类精简
			if (_historyView == null) _historyView = new Parts.HistoryManagerTabView();
			_historyView.Draw(inRect, _controller.State, _history, _recap, convKey =>
			{
				try
				{
					var parts = _history.GetParticipantsOrEmpty(convKey) ?? new System.Collections.Generic.List<string>();
					var ids = new System.Collections.Generic.List<string>(parts); ids.Sort(System.StringComparer.Ordinal);
					_controller = new ChatController(_llm, _history, _world, _orchestration, _prompting, convKey, ids);
					_controller.State.Messages.Clear(); _historyWritten = false; _ = _controller.StartAsync();
					BackToChatAndRefresh();
				}
				catch { }
			});
		}

		private Parts.HistoryManagerTabView _historyView;
		private Parts.PersonaTabView _personaView;

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
			// 直接弹出编辑窗口，避免在主窗内嵌长输入控件；保持简单
			// 根据 pawn id 生成 entityId
			string entityId = _pawn != null && _pawn.thingIDNumber != 0 ? ($"pawn:{_pawn.thingIDNumber}") : null;
			if (string.IsNullOrEmpty(entityId))
			{
				Widgets.Label(inRect, "RimAI.ChatUI.Common.NoPawnSelected".Translate());
				return;
			}
			Find.WindowStack.Add(new Parts.FixedPromptEditor(entityId, _persona));
			// 回到历史页
			_activeTab = ChatTab.History;
		}

		private void DrawTitleSettingsTab(Rect inRect)
		{
			var cfg = _container.Resolve<IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
			string current = null; try { current = cfg?.GetPlayerTitleOrDefault() ?? "RimAI.ChatUI.PlayerTitle.Default".Translate(); } catch { current = "RimAI.ChatUI.PlayerTitle.Default".Translate(); }
			// 文本框 + 保存/重置（不在此处覆盖 _titleInputText，避免用户清空时被重置）
			Text.Font = GameFont.Medium; Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 28f), "RimAI.ChatUI.TitleSettings.Question".Translate()); Text.Font = GameFont.Small;
			var box = new Rect(inRect.x, inRect.y + 36f, Mathf.Min(260f, inRect.width - 20f), 28f);
			// 独立的称谓单行输入框，不与聊天输入共享
			_titleInputText = Widgets.TextField(box, _titleInputText ?? string.Empty);
			var btnY = box.yMax + 8f;
			if (Widgets.ButtonText(new Rect(inRect.x, btnY, 90f, 26f), "RimAI.Common.Save".Translate())) 
			{ 
				try 
				{ 
					var toSave = string.IsNullOrWhiteSpace(_titleInputText) ? (current ?? "RimAI.ChatUI.PlayerTitle.Default".Translate()) : _titleInputText.Trim();
					cfg?.SetPlayerTitle(toSave); 
					_controller.State.PlayerTitle = cfg?.GetPlayerTitleOrDefault() ?? "RimAI.ChatUI.PlayerTitle.Default".Translate(); 
				} 
				catch { } 
			}
			if (Widgets.ButtonText(new Rect(inRect.x + 100f, btnY, 90f, 26f), "RimAI.Common.Reset".Translate())) { try { cfg?.SetPlayerTitle("RimAI.ChatUI.PlayerTitle.Default".Translate()); _titleInputText = cfg?.GetPlayerTitleOrDefault() ?? "RimAI.ChatUI.PlayerTitle.Default".Translate(); _controller.State.PlayerTitle = _titleInputText; } catch { } }
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
				var sz = new Vector2(size, size);
				_pawnPortrait = PortraitsCache.Get(_pawn, sz, Rot4.South);
			}
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
	}
}


