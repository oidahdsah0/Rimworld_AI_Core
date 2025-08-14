using System;
using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.History.Models
{
	internal enum EntryRole { User, Ai }

	internal enum RecapMode { Replace, Append }

	internal sealed class HistoryEntry
	{
		public string Id { get; set; }
		public EntryRole Role { get; set; }
		public string Content { get; set; }
		public DateTime Timestamp { get; set; }
		public bool Deleted { get; set; }
		public long? TurnOrdinal { get; set; } // 仅 AI 最终输出持有“单调回合序号”
		public DateTime? DeletedAt { get; set; }
	}

	internal sealed class HistoryThread
	{
		public string ConvKey { get; set; }
		public IReadOnlyList<HistoryEntry> Entries { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalEntries { get; set; }
	}

	internal sealed class RecapItem
	{
		public string Id { get; set; }
		public string ConvKey { get; set; }
		public RecapMode Mode { get; set; }
		public string Text { get; set; }
		public int MaxChars { get; set; }
		public long FromTurnExclusive { get; set; }
		public long ToTurnInclusive { get; set; }
		public bool Stale { get; set; }
		public string IdempotencyKey { get; set; }
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}

	internal sealed class RelationResult
	{
		public IReadOnlyList<string> ConvKeys { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int Total { get; set; }
	}
}


