namespace RimAI.Core.Source.Infrastructure.Configuration
{
    // Internal configuration model, may contain more fields than the external snapshot.
    public sealed class CoreConfig
    {
        public string Version { get; set; } = "v5-P1";

        public GeneralSection General { get; set; } = new();
        public DiagnosticsSection Diagnostics { get; set; } = new();
        public UiSection UI { get; set; } = new();

        // Placeholders for future phases to keep shape stable
        public object Prompt { get; set; } = new();
        public HistorySection History { get; set; } = new();
        public StageSection Stage { get; set; } = new();
        public object Orchestration { get; set; } = new();
        public object Embedding { get; set; } = new();

		// P2 internal config node for LLM Gateway
		public LlmSection LLM { get; set; } = new();

		// P3 internal config nodes
		public SchedulerSection Scheduler { get; set; } = new();
		public WorldDataSection WorldData { get; set; } = new();

		// P4 internal config node for Tooling (仅内部使用，不暴露到 Snapshot)
		public ToolingSection Tooling { get; set; } = new();

		// P6 internal config node for Persistence (仅内部使用，不暴露到 Snapshot)
		public PersistenceSection Persistence { get; set; } = new();

		// P7 internal config node for Persona（仅内部使用，不暴露到 Snapshot）
		public PersonaSection Persona { get; set; } = new();

        public sealed class GeneralSection
        {
            public string Locale { get; set; } = "en";
            // null 表示自动跟随游戏语言；非空则固定为该 locale
            public string PromptLocaleOverride { get; set; } = null;
        }

        public sealed class DiagnosticsSection
        {
            public bool VerboseLogs { get; set; } = false;
        }

        public sealed class UiSection
        {
            public bool DebugPanelEnabled { get; set; } = true;
            public ChatWindowSection ChatWindow { get; set; } = new();
        }

        public sealed class ChatWindowSection
        {
            public ChatPromptsSection Prompts { get; set; } = new();
            public string PlayerTitle { get; set; } = "总督";
        }

        public sealed class ChatPromptsSection
        {
            public string Locale { get; set; } = null; // null=follow General.Locale
            public int MaxSystemPromptChars { get; set; } = 1600;
            public int MaxBlocksChars { get; set; } = 2400;
            public SocialSection Social { get; set; } = new();
            public EnvSection Env { get; set; } = new();
            public ComposersSection Composers { get; set; } = new();
        }

        public sealed class SocialSection
        {
            public int TopRelations { get; set; } = 5;
            public int RecentEvents { get; set; } = 5;
        }

        public sealed class EnvSection
        {
            public int Radius { get; set; } = 9; // 以格为单位，默认为 9 格半径（19x19）
        }

        public sealed class ComposersSection
        {
            public ComposerGroup ChatUI { get; set; } = new();
        }

        public sealed class ComposerGroup
        {
            public string[] Enabled { get; set; } = System.Array.Empty<string>();
            public string[] Order { get; set; } = System.Array.Empty<string>();
        }

		public sealed class LlmSection
		{
			public string Locale { get; set; } = "zh-Hans";
			public int DefaultTimeoutMs { get; set; } = 15000;
			public StreamSection Stream { get; set; } = new();
			public RetrySection Retry { get; set; } = new();
			public CircuitSection CircuitBreaker { get; set; } = new();
			public BatchSection Batch { get; set; } = new();
		}

		public sealed class StreamSection { public int HeartbeatTimeoutMs { get; set; } = 15000; public int LogEveryNChunks { get; set; } = 20; }
		public sealed class RetrySection { public int MaxAttempts { get; set; } = 3; public int BaseDelayMs { get; set; } = 400; }
		public sealed class CircuitSection { public double ErrorThreshold { get; set; } = 0.5; public int WindowMs { get; set; } = 60000; public int CooldownMs { get; set; } = 60000; }
		public sealed class BatchSection { public int MaxConcurrent { get; set; } = 4; }

		public sealed class SchedulerSection
		{
			public int MaxTasksPerUpdate { get; set; } = 64;
			public double MaxBudgetMsPerUpdate { get; set; } = 0.5;
			public int MaxQueueLength { get; set; } = 2000;
			public int LongTaskWarnMs { get; set; } = 5;
			public bool EnablePriorityQueue { get; set; } = false;
		}

		public sealed class WorldDataSection
		{
			public int DefaultTimeoutMs { get; set; } = 2000;
			public string NameFallbackLocale { get; set; } = "zh-Hans";
		}

		public sealed class ToolingSection
		{
			public bool Enabled { get; set; } = true;
			public int DefaultTimeoutMs { get; set; } = 3000;
			public int MaxConcurrent { get; set; } = 8;
			public string[] Whitelist { get; set; } = System.Array.Empty<string>();
			public string[] Blacklist { get; set; } = System.Array.Empty<string>();
			public bool DangerousToolConfirmation { get; set; } = false;
			// 新增：工具调用模式（默认 Classic）
			public RimAI.Core.Contracts.Config.ToolCallMode ToolCallMode { get; set; } = RimAI.Core.Contracts.Config.ToolCallMode.Classic;
			public EmbeddingSection Embedding { get; set; } = new();
			public NarrowTopKSection NarrowTopK { get; set; } = new();
			public IndexFilesSection IndexFiles { get; set; } = new();
		}

		public sealed class EmbeddingSection
		{
			public string Provider { get; set; } = "auto";
			public string Model { get; set; } = "auto";
			public int Dimension { get; set; } = 0;
			public string Instruction { get; set; } = string.Empty;
			public WeightSection Weights { get; set; } = new();
			public bool AutoBuildOnStart { get; set; } = true;
			public bool BlockDuringBuild { get; set; } = true;
			public int MaxParallel { get; set; } = 4;
			public int MaxPerMinute { get; set; } = 120;
		}

		public sealed class WeightSection { public double Name { get; set; } = 0.6; public double Desc { get; set; } = 0.4; public double Params { get; set; } = 0.0; }
		public sealed class NarrowTopKSection { public int TopK { get; set; } = 5; public double MinScoreThreshold { get; set; } = 0.0; }
		public sealed class IndexFilesSection { public string BasePath { get; set; } = "Config/RimAI/Indices"; public string FileNameFormat { get; set; } = "tools_index_{provider}_{model}.json"; }

		public sealed class PersistenceSection
		{
			public int MaxTextLength { get; set; } = 4000;
			public bool EnableDebugExport { get; set; } = true;
			public int NodeTimeoutMs { get; set; } = 200;
			public bool OnLoadRebuildIndexes { get; set; } = true;
			public FilesSection Files { get; set; } = new();
		}

		public sealed class FilesSection
		{
			public string BasePath { get; set; } = "Config/RimAI";
			public string IndicesPath { get; set; } = "Config/RimAI/Indices";
		}

		public sealed class PersonaSection
		{
			public string Locale { get; set; } = "zh-Hans";
			public PersonaBudgetSection Budget { get; set; } = new();
			public PersonaGenerationSection Generation { get; set; } = new();
			public PersonaTemplatesSection Templates { get; set; } = new();
			public PersonaUiSection UI { get; set; } = new();
		}

		public sealed class PersonaBudgetSection
		{
			public int MaxTotalChars { get; set; } = 4000;
			public int Job { get; set; } = 600;
			public int Fixed { get; set; } = 800;
			public int IdeologySegment { get; set; } = 600;
			public int BiographyPerItem { get; set; } = 400;
			public int BiographyMaxItems { get; set; } = 4;
		}

		public sealed class PersonaGenerationSection
		{
			public int TimeoutMs { get; set; } = 15000;
			public RetrySection Retry { get; set; } = new();
		}

		public sealed class PersonaTemplatesSection
		{
			public string MasterPath { get; set; } = "Resources/prompts/persona/{locale}.persona.json";
			public string UserOverridePath { get; set; } = "Config/RimAI/Prompts/persona/{locale}.persona.user.json";
			public bool HotReload { get; set; } = true;
		}

		public sealed class PersonaUiSection
		{
			public bool EnableExtractFixedFromExisting { get; set; } = true;
		}

        // P8 internal config node for History (仅内部使用，不暴露到 Snapshot)
        public sealed class HistorySection
        {
            public int SummaryEveryNRounds { get; set; } = 5;
            public RecapSection Recap { get; set; } = new();
            public int PageSize { get; set; } = 100;
            public int UndoWindowSeconds { get; set; } = 3;
            public HistoryBudgetSection Budget { get; set; } = new();
            public int DisplayNameCacheSize { get; set; } = 256;
        }

        public sealed class RecapSection
        {
            public string Mode { get; set; } = "Append"; // Replace | Append
            public int MaxChars { get; set; } = 1200;
            public bool AutoRebuildOnEdit { get; set; } = false;
            public int ManualLastEntries { get; set; } = 12; // 手动生成窗口：最近 N 条历史（user/ai 混合计数）
        }

        public sealed class HistoryBudgetSection
        {
            public int MaxLatencyMs { get; set; } = 5000;
        }

		// P9 internal config node for Stage（仅内部使用，不暴露到 Snapshot）
		public sealed class StageSection
		{
			public int CoalesceWindowMs { get; set; } = 300;
			public int CooldownSeconds { get; set; } = 30;
			public int MaxRunning { get; set; } = 4;
			public int LeaseTtlMs { get; set; } = 10000;
			public int IdempotencyTtlMs { get; set; } = 60000;
			public string LocaleOverride { get; set; } = null;
			public StageHistoryLoggingSection History { get; set; } = new();
			public StageLoggingSection Logging { get; set; } = new();
			public string[] DisabledActs { get; set; } = System.Array.Empty<string>();
			public string[] DisabledTriggers { get; set; } = System.Array.Empty<string>();
			public ActsSection Acts { get; set; } = new();
		}

		public sealed class StageHistoryLoggingSection
		{
			public string StageLogConvKey { get; set; } = "agent:stage";
			public int MaxFinalTextChars { get; set; } = 800;
			public bool HeaderEnabled { get; set; } = true;
		}

		public sealed class StageLoggingSection
		{
			public int PayloadPreviewChars { get; set; } = 200;
			public int SlowActWarnMs { get; set; } = 5000;
		}

		public sealed class ActsSection
		{
			public GroupChatActSection GroupChat { get; set; } = new();
			public AlphaFiberActSection AlphaFiberInterServerChat { get; set; } = new();
		}

		public sealed class GroupChatActSection
		{
			public int Rounds { get; set; } = 1;
			public int MaxLatencyMsPerTurn { get; set; } = 8000;
			public string SeedPolicy { get; set; } = "ConvKeyWithTick";
			public int GatherRadius { get; set; } = 3; // 将非发起者移动至发起者周围的半径（格）
			public int BubbleDelayMs { get; set; } = 1000; // 每条气泡之间的固定延迟（真实毫秒）
			public int RoundIntervalMinMs { get; set; } = 0; // 轮次间最小间隔（真实毫秒）
			public int RoundIntervalMaxMs { get; set; } = 1000; // 轮次间最大间隔（真实毫秒）
			public int MaxSessionDurationMs { get; set; } = 60000; // 会话最长持续时间（真实毫秒）
		}

		public sealed class AlphaFiberActSection
		{
			public int ScanIntervalSeconds { get; set; } = 600;
			public string Pairing { get; set; } = "Random";
			public int MaxPairsPerScan { get; set; } = 1;
		}
    }
}


