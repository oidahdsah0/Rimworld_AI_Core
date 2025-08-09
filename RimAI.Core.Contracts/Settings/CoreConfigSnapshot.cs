namespace RimAI.Core.Contracts.Settings
{
    /// <summary>
    /// 对外暴露的最小配置快照。不得依赖 Verse/Unity 类型。
    /// </summary>
    public sealed class CoreConfigSnapshot
    {
        public LLMConfigSnapshot LLM { get; set; } = new LLMConfigSnapshot();
        public OrchestrationConfigSnapshot Orchestration { get; set; } = new OrchestrationConfigSnapshot();
        public EmbeddingConfigSnapshot Embedding { get; set; } = new EmbeddingConfigSnapshot();
    }

    public sealed class LLMConfigSnapshot
    {
        public double Temperature { get; set; }
    }

    public sealed class OrchestrationConfigSnapshot
    {
        public string Strategy { get; set; } = "Classic";
        public OrchestrationProgressConfigSnapshot Progress { get; set; } = new OrchestrationProgressConfigSnapshot();
        public PlanningConfigSnapshot Planning { get; set; } = new PlanningConfigSnapshot();
    }

    public sealed class OrchestrationProgressConfigSnapshot
    {
        public string DefaultTemplate { get; set; } = "[{{Source}}] {{Stage}}: {{Message}}";
        public System.Collections.Generic.Dictionary<string, string> StageTemplates { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        public int PayloadPreviewChars { get; set; } = 200;
    }

    public sealed class PlanningConfigSnapshot
    {
        public bool EnableLightChaining { get; set; }
        public int MaxSteps { get; set; }
        public bool AllowParallel { get; set; }
        public int MaxParallelism { get; set; }
        public int FanoutPerStage { get; set; }
        public double SatisfactionThreshold { get; set; }
    }

    public sealed class EmbeddingConfigSnapshot
    {
        public bool Enabled { get; set; }
        public int TopK { get; set; }
        public int MaxContextChars { get; set; }
        public EmbeddingToolsConfigSnapshot Tools { get; set; } = new EmbeddingToolsConfigSnapshot();
    }

    public sealed class EmbeddingToolsConfigSnapshot
    {
        public string Mode { get; set; } = "FastTop1";
        public double Top1Threshold { get; set; }
        public double LightningTop1Threshold { get; set; }
        public string IndexPath { get; set; } = "auto";
        public bool AutoBuildOnStart { get; set; } = true;
        public bool BlockDuringBuild { get; set; } = true;
        public EmbeddingToolsScoreWeightsSnapshot ScoreWeights { get; set; } = new EmbeddingToolsScoreWeightsSnapshot();
        public EmbeddingDynamicThresholdsSnapshot DynamicThresholds { get; set; } = new EmbeddingDynamicThresholdsSnapshot();
    }

    public sealed class EmbeddingToolsScoreWeightsSnapshot
    {
        public double Name { get; set; }
        public double Description { get; set; }
    }

    public sealed class EmbeddingDynamicThresholdsSnapshot
    {
        public bool Enabled { get; set; }
        public double Smoothing { get; set; }
        public double MinTop1 { get; set; }
        public double MaxTop1 { get; set; }
    }
}


