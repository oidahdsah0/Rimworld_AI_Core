using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Modules.History;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.UI.ChatWindow.Parts;

namespace RimAI.Core.Source.UI.ChatWindow
{
	public sealed class ChatWindow : Window
	{
		private readonly ServiceContainer _container;
		private readonly ILLMService _llm;
		private readonly IHistoryService _history;
		private readonly IWorldDataService _world;
		private readonly IOrchestrationService _orchestration;

		private readonly Pawn _pawn;
		private ChatController _controller;
		private ChatTab _activeTab = ChatTab.History;
		private string _inputText = string.Empty;
		private Vector2 _scrollTranscript = Vector2.zero;
		private Vector2 _scrollRight = Vector2.zero;
        private bool _historyWritten;
		private static string _cachedPlayerId;
		private float _lastTranscriptContentHeight;

		public override Vector2 InitialSize => new Vector2(900f, 600f);

		public ChatWindow(Pawn pawn)
		{
			_pawn = pawn;
			doCloseX = true;
			draggable = true;
			preventCameraMotion = false;
			absorbInputAroundWindow = true;
			closeOnClickedOutside = false;
			// 确保 Enter/Escape 不会触发默认 Close 行为，由我们自行处理
			closeOnAccept = false;
			closeOnCancel = false;

			_container = RimAICoreMod.Container;
			_llm = _container.Resolve<ILLMService>();
			_history = _container.Resolve<IHistoryService>();
			_world = _container.Resolve<IWorldDataService>();
			_orchestration = _container.Resolve<IOrchestrationService>();

			var participantIds = BuildParticipantIds(pawn);
			var convKey = BuildConvKey(participantIds);
			_controller = new ChatController(_llm, _history, _world, _orchestration, convKey, participantIds);
		}

		public override void DoWindowContents(Rect inRect)
		{
			var leftW = inRect.width * (1f / 6f);
			var rightW = inRect.width - leftW - 8f;
			var leftRect = new Rect(inRect.x, inRect.y, leftW, inRect.height);
			var rightRectOuter = new Rect(leftRect.xMax + 8f, inRect.y, rightW, inRect.height);
			var titleH = 48f;
			var indicatorH = 20f;
			var inputH = 60f; // 输入栏高度与按钮同高
			var titleRect = new Rect(rightRectOuter.x, rightRectOuter.y, rightRectOuter.width, titleH);
			var transcriptRect = new Rect(rightRectOuter.x, titleRect.yMax + 4f, rightRectOuter.width, rightRectOuter.height - titleH - indicatorH - inputH - 26f);
			var indicatorRect = new Rect(rightRectOuter.x, transcriptRect.yMax + 4f, rightRectOuter.width, indicatorH);
			var inputRect = new Rect(rightRectOuter.x, indicatorRect.yMax + 12f, rightRectOuter.width, inputH);

			// 左列
			LeftSidebarCard.Draw(leftRect, ref _activeTab, null, _pawn?.LabelCap ?? "Pawn", GetJobTitleOrNone(_pawn));

			// 右列
			TitleBar.Draw(titleRect, null, _pawn?.LabelCap ?? "Pawn", GetJobTitleOrNone(_pawn));

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
				var text = BuildLcdText();
				Parts.LcdMarquee.Draw(lcdRect, _controller.State.Lcd, text, pulse, _controller.State.IsStreaming);
			}
			InputRow.Draw(inputRect, ref _inputText,
				onSmalltalk: () => _ = OnSendSmalltalkAsync(),
				onCommand: () => _ = OnSendCommandAsync(),
				onCancel: () => OnCancelStreaming(),
				isStreaming: _controller.State.IsStreaming);

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

		private static string GetJobTitleOrNone(Pawn pawn)
		{
			return "无职务"; // 占位：后续接入 Persona Job Service
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
	}
}


