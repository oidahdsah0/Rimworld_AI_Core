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
    }
}


