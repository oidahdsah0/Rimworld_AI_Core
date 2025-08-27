using System.Collections.Generic;
using Newtonsoft.Json;

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
		// P13: Servers state (AI 服务器建筑数据与巡检计划)
		public ServerState Servers { get; set; } = new ServerState();
		// Unknown Civilization relationship state (favor/cooldown etc.)
		public UnknownCivState UnknownCiv { get; set; } = new UnknownCivState();
		// Weather control state (cooldown and bookkeeping)
		public WeatherControlState WeatherControl { get; set; } = new WeatherControlState();
		// Subspace invocation state (cooldown and bookkeeping)
		public SubspaceInvocationState SubspaceInvocation { get; set; } = new SubspaceInvocationState();
	}

	public sealed class UnknownCivState
	{
		public int Favor { get; set; } = 0;
		public int NextGiftAllowedAbsTicks { get; set; } = 0; // 允许下次赠礼的绝对 Tick（Find.TickManager.TicksAbs）
		public int LastGiftAtAbsTicks { get; set; } = 0;
	}

	public sealed class WeatherControlState
	{
		public string LastAppliedWeather { get; set; } = string.Empty;
		public int LastAppliedAtAbsTicks { get; set; } = 0;
		public int ExpectedEndAtAbsTicks { get; set; } = 0;
		public int NextAllowedAtAbsTicks { get; set; } = 0; // cooldown gate
	}

	public sealed class SubspaceInvocationState
	{
		public int LastInvokedAtAbsTicks { get; set; } = 0;
		public int NextAllowedAtAbsTicks { get; set; } = 0; // cooldown gate
		public string LastTier { get; set; } = string.Empty; // low|mid|high|apex
		public string LastComposition { get; set; } = string.Empty; // insects|shamblers|revenant|mixed
		public int LastCount { get; set; } = 0;
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

	// P13: Servers snapshot models
	public sealed class ServerState
	{
		public Dictionary<string, ServerRecord> Items { get; set; } = new Dictionary<string, ServerRecord>();
	}

	public sealed class ServerRecord
	{
		// 基础信息
		public string EntityId { get; set; } = string.Empty; // thing:<loadId>
		public int Level { get; set; } // 1..3
		public string SerialHex12 { get; set; } = string.Empty; // 12位16进制，A..F 大写
		public int BuiltAtAbsTicks { get; set; } // 建成绝对 Tick（60k=1天）

		// 基础人格（兼容字段：若 ServerPersonaSlots 为空，则回退使用 BaseServerPersona*）
		[JsonProperty("BasePersonaPresetKey")] // 兼容旧字段名
		public string BaseServerPersonaPresetKey { get; set; } // 可空
		[JsonProperty("BasePersonaOverride")] // 兼容旧字段名
		public string BaseServerPersonaOverride { get; set; } // 可空

		// 人格槽位（按等级容量：Lv1=1/Lv2=2/Lv3=3）
		[JsonProperty("PersonaSlots")] // 兼容旧字段名
		public List<ServerPersonaSlot> ServerPersonaSlots { get; set; } = new List<ServerPersonaSlot>();

		// 巡检计划
		public int InspectionIntervalHours { get; set; } = 24; // 默认 24；最小 6
		public bool InspectionEnabled { get; set; } = false; // 是否启用巡检（UI 可切换），默认关闭
		public int NextSlotPointer { get; set; } = 0; // 下次从哪个槽位开始轮询（0..cap-1）
		public List<InspectionSlot> InspectionSlots { get; set; } = new List<InspectionSlot>();

		// 最近一次汇总
		public string LastSummaryText { get; set; }
		public int? LastSummaryAtAbsTicks { get; set; }
	}

	public sealed class InspectionSlot
	{
		public int Index { get; set; }
		public string ToolName { get; set; }
		public bool Enabled { get; set; } = true;
		public int? LastRunAbsTicks { get; set; }
		public int? NextDueAbsTicks { get; set; }
	}

	public sealed class ServerPersonaSlot
	{
		public int Index { get; set; } // 0..(capacity-1)
		public string PresetKey { get; set; } // 预设键，必须存在于 BaseOptions
		public string OverrideText { get; set; } // 可选覆盖文本
		public bool Enabled { get; set; } = true;
	}
}


