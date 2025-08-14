using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Persistence.Snapshots
{
	public sealed class PersistenceSnapshot
	{
		public HistoryState History { get; set; } = new HistoryState();
		public RecapState Recap { get; set; } = new RecapState();
		public FixedPromptsSnapshot FixedPrompts { get; set; } = new FixedPromptsSnapshot();
		public PersonaJobState PersonaJob { get; set; } = new PersonaJobState();
		public BiographySnapshot Biographies { get; set; } = new BiographySnapshot();
		public PersonaBindingsSnapshot PersonaBindings { get; set; } = new PersonaBindingsSnapshot();
		public PersonalBeliefsState PersonalBeliefs { get; set; } = new PersonalBeliefsState();
		public StageRecapState StageRecap { get; set; } = new StageRecapState();
	}

	public sealed class HistoryState
	{
		public Dictionary<string, ConversationRecord> Conversations { get; set; } = new Dictionary<string, ConversationRecord>();
		public Dictionary<string, List<string>> ConvKeyIndex { get; set; } = new Dictionary<string, List<string>>();
		public Dictionary<string, List<string>> ParticipantIndex { get; set; } = new Dictionary<string, List<string>>();
	}

	public sealed class ConversationRecord
	{
		public List<string> ParticipantIds { get; set; } = new List<string>();
		public List<ConversationEntry> Entries { get; set; } = new List<ConversationEntry>();
	}

	public sealed class ConversationEntry
	{
		public string Role { get; set; } = string.Empty; // "user" | "ai"
		public string Text { get; set; } = string.Empty;
		public long CreatedAtTicksUtc { get; set; }
		public long? TurnOrdinal { get; set; }
	}

	public sealed class RecapState
	{
		public Dictionary<string, List<RecapSnapshotItem>> Recaps { get; set; } = new Dictionary<string, List<RecapSnapshotItem>>();
	}

	public sealed class RecapSnapshotItem
	{
		public string Id { get; set; } = string.Empty;
		public string Text { get; set; } = string.Empty;
		public long CreatedAtTicksUtc { get; set; }
		// P8: additional fields for idempotency and window tracking
		public string IdempotencyKey { get; set; } = string.Empty;
		public long FromTurnExclusive { get; set; }
		public long ToTurnInclusive { get; set; }
		public string Mode { get; set; } = "Append";
	}

	public sealed class FixedPromptsSnapshot
	{
		public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
	}

	public sealed class PersonaJobState
	{
		public Dictionary<string, PersonaJob> Items { get; set; } = new Dictionary<string, PersonaJob>();
	}

	public sealed class PersonaJob
	{
		public string Name { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public long UpdatedAtTicksUtc { get; set; }
	}

	public sealed class BiographySnapshot
	{
		public Dictionary<string, List<BiographyItem>> Items { get; set; } = new Dictionary<string, List<BiographyItem>>();
	}

	public sealed class BiographyItem
	{
		public string Id { get; set; } = string.Empty;
		public string Text { get; set; } = string.Empty;
		public long CreatedAtTicksUtc { get; set; }
		public string Source { get; set; } = string.Empty;
	}

	public sealed class PersonaBindingsSnapshot
	{
		public Dictionary<string, string> Items { get; set; } = new Dictionary<string, string>();
	}

	public sealed class PersonalBeliefsState
	{
		public Dictionary<string, PersonalBeliefs> Items { get; set; } = new Dictionary<string, PersonalBeliefs>();
	}

	public sealed class PersonalBeliefs
	{
		public string Worldview { get; set; } = string.Empty;
		public string Values { get; set; } = string.Empty;
		public string CodeOfConduct { get; set; } = string.Empty;
		public string TraitsText { get; set; } = string.Empty;
	}

	public sealed class StageRecapState
	{
		public List<ActRecapEntry> Items { get; set; } = new List<ActRecapEntry>();
	}

	public sealed class ActRecapEntry
	{
		public string Title { get; set; } = string.Empty;
		public long TriggerAtTicksUtc { get; set; }
		public string ActName { get; set; } = string.Empty;
		public string ConvKey { get; set; } = string.Empty;
		public List<string> Participants { get; set; } = new List<string>();
		public string SummaryText { get; set; } = string.Empty;
		public string MetadataJson { get; set; } = string.Empty;
		public List<string> Tags { get; set; } = new List<string>();
		public long CreatedAtTicksUtc { get; set; }
	}
}


