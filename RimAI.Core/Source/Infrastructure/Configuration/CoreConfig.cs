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
        public object History { get; set; } = new();
        public object Stage { get; set; } = new();
        public object Orchestration { get; set; } = new();
		public object Embedding { get; set; } = new();

		// P2 internal config node for LLM Gateway
		public LlmSection LLM { get; set; } = new();

		// P3 internal config nodes
		public SchedulerSection Scheduler { get; set; } = new();
		public WorldDataSection WorldData { get; set; } = new();

        public sealed class GeneralSection
        {
            public string Locale { get; set; } = "zh-Hans";
        }

        public sealed class DiagnosticsSection
        {
            public bool VerboseLogs { get; set; } = false;
        }

        public sealed class UiSection
        {
            public bool DebugPanelEnabled { get; set; } = true;
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
    }
}


