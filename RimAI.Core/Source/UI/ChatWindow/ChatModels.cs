using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RimAI.Core.Source.UI.ChatWindow
{
	internal enum ChatTab
	{
		History,
		Persona,
		Job,
		FixedPrompt,
		HistoryAdmin,
		Title
	}

	internal enum MessageSender
	{
		User,
		Ai
	}

	internal sealed class ChatMessage
	{
		public string Id { get; set; }
		public MessageSender Sender { get; set; }
		public string DisplayName { get; set; }
		public DateTime TimestampUtc { get; set; }
		public string Text { get; set; }
		public bool IsCommand { get; set; }
	}

	internal sealed class IndicatorLightsState
	{
		public bool DataOn { get; set; }
		public bool FinishOn { get; set; }
		public DateTime DataBlinkUntilUtc { get; set; }
		public DateTime DataNextAllowedBlinkUtc { get; set; }
	}

	internal sealed class ChatConversationState
	{
		public string ConvKey { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public List<ChatMessage> Messages { get; } = new List<ChatMessage>();
		public IndicatorLightsState Indicators { get; } = new IndicatorLightsState();
		public ConcurrentQueue<string> StreamingChunks { get; } = new ConcurrentQueue<string>();
		public ConcurrentQueue<ChatMessage> PendingInitMessages { get; } = new ConcurrentQueue<ChatMessage>();
		public LcdMarqueeState Lcd { get; } = new LcdMarqueeState();
		public bool IsStreaming { get; set; }
		public string LastUserInputStash { get; set; }
		public int ActiveStreamId { get; set; }
		public string PlayerTitle { get; set; }
		public bool FinalCommittedThisTurn { get; set; }
	}

	internal sealed class LcdMarqueeState
	{
		public string CachedText { get; set; } = string.Empty; // 当前已展开的文案（用于比较是否需要重建列）
		public List<byte> Columns { get; } = new List<byte>(); // 逐列 7 位点阵（低位为顶部像素）
		public List<bool> ColumnIsGreen { get; } = new List<bool>(); // 每列是否绿色（否则红色），实现红绿间隔
		public float OffsetPx { get; set; } = 0f;  // 当前像素偏移
		public float StepPx { get; set; } = 1f;    // 每次步进的像素数
		public double NextStepAtRealtime { get; set; } = 0.0; // 下一次推进的实时秒（Time.realtimeSinceStartup）
		public float IntervalSec { get; set; } = 0.75f; // 每步间隔秒（按秒节拍的整像素滚动）
	}
}


