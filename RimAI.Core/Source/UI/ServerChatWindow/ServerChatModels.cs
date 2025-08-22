using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RimAI.Core.Source.UI.ServerChatWindow
{
	internal enum ServerChatTab
	{
		History,
		Chat,
		Persona,
		InterServerComms
	}

	internal enum ServerMessageSender
	{
		User,
		Ai
	}

	internal sealed class ServerChatMessage
	{
		public string Id { get; set; }
		public ServerMessageSender Sender { get; set; }
		public string DisplayName { get; set; }
		public DateTime TimestampUtc { get; set; }
		public string Text { get; set; }
		public bool IsCommand { get; set; }
	}

	internal sealed class ServerIndicatorLightsState
	{
		public bool DataOn { get; set; }
		public bool FinishOn { get; set; }
		public DateTime DataBlinkUntilUtc { get; set; }
		public DateTime DataNextAllowedBlinkUtc { get; set; }
	}

	internal sealed class TemperatureSeriesState
	{
		public int Capacity { get; }
		public readonly Queue<float> Samples = new Queue<float>();
		public TemperatureSeriesState(int capacity = 20) { Capacity = Math.Max(5, capacity); }
		public void Push(float value)
		{
			Samples.Enqueue(value);
			while (Samples.Count > Capacity) Samples.Dequeue();
		}
	}

	internal sealed class ServerChatConversationState
	{
		public string ConvKey { get; set; }
		public IReadOnlyList<string> ParticipantIds { get; set; }
		public List<ServerChatMessage> Messages { get; } = new List<ServerChatMessage>();
		public ConcurrentQueue<ServerChatMessage> PendingInitMessages { get; } = new ConcurrentQueue<ServerChatMessage>();
		public ServerIndicatorLightsState Indicators { get; } = new ServerIndicatorLightsState();
		public bool IsBusy { get; set; }
		public string LastUserInputStash { get; set; }
		public string PlayerTitle { get; set; }
		public string SelectedServerEntityId { get; set; }
		public TemperatureSeriesState TemperatureSeries { get; } = new TemperatureSeriesState();
	}
}


