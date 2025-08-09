namespace RimAI.Core.Settings
{
    /// <summary>
    /// 顶级配置对象，包含所有子配置。属性设为 init 只读，以保证不可变性。
    /// </summary>
    public sealed class CoreConfig
    {
        public LLMConfig LLM { get; init; } = new();
        public EventAggregatorConfig EventAggregator { get; init; } = new();
        public OrchestrationConfig Orchestration { get; init; } = new();
        public EmbeddingConfig Embedding { get; init; } = new();
        public HistoryConfig History { get; init; } = new();

        public static CoreConfig CreateDefault() => new();
    }

    /// <summary>
    /// 事件聚合器配置。
    /// </summary>
    public sealed class EventAggregatorConfig
    {
        /// <summary>
        /// 事件处理循环的间隔时间（分钟）。
        /// </summary>
        public double ProcessingIntervalMinutes { get; init; } = 1.0;

        /// <summary>
        /// 触发 LLM 调用后的冷却时间（分钟）。
        /// </summary>
        public double CooldownMinutes { get; init; } = 5.0;

        /// <summary>
        /// 在处理之前，缓冲区中可以容纳的最大事件数。
        /// </summary>
        public int MaxBufferSize { get; init; } = 20;
    }

    /// <summary>
    /// 大模型相关配置。
    /// </summary>
    public sealed class LLMConfig
    {
        /// <summary>
        /// 默认 Temperature，取值 0~2。
        /// </summary>
        public double Temperature { get; init; } = 0.7;

        /// <summary>
        /// OpenAI / 其他服务商 API Key。
        /// </summary>
        public string ApiKey { get; init; } = string.Empty;
    }

    // 缓存配置已下沉至 Framework；Core 不再维护通用缓存 TTL

    /// <summary>
    /// 编排层配置。
    /// </summary>
    public sealed class OrchestrationConfig
    {
        /// <summary>
        /// 策略选择：Classic | EmbeddingFirst。
        /// </summary>
        public string Strategy { get; init; } = "Classic";

        /// <summary>
        /// 轻量规划器设置。
        /// </summary>
        public PlanningConfig Planning { get; init; } = new();

        /// <summary>
        /// 编排进度反馈与可视化模板设置。
        /// </summary>
        public OrchestrationProgressConfig Progress { get; init; } = new();
    }

    public sealed class PlanningConfig
    {
        public bool EnableLightChaining { get; init; } = false;
        public int MaxSteps { get; init; } = 3;
        public bool AllowParallel { get; init; } = false;
        public int MaxParallelism { get; init; } = 2;
        public int FanoutPerStage { get; init; } = 3;
        public double SatisfactionThreshold { get; init; } = 0.8;
    }

    /// <summary>
    /// 编排进度反馈模板配置。
    /// 支持按 Stage 定制模板，未命中使用默认模板。
    /// 占位符：{Source} / {Stage} / {Message}
    /// </summary>
    public sealed class OrchestrationProgressConfig
    {
        public string DefaultTemplate { get; init; } = "{Stage}: {Message}";
        public System.Collections.Generic.Dictionary<string, string> StageTemplates { get; init; } = new();
        public int PayloadPreviewChars { get; init; } = 200;
    }

    /// <summary>
    /// Embedding/RAG 相关配置（最小）。
    /// </summary>
    public sealed class EmbeddingConfig
    {
        public bool Enabled { get; init; } = true;
        public int TopK { get; init; } = 5;
        public int MaxContextChars { get; init; } = 2000;
        public EmbeddingToolsConfig Tools { get; init; } = new();
    }

    /// <summary>
    /// 工具向量库与匹配模式配置（预留，S2 仅占位）。
    /// </summary>
    public sealed class EmbeddingToolsConfig
    {
        public string Mode { get; init; } = "FastTop1"; // 默认开启 FastTop1
        public double Top1Threshold { get; init; } = 0.82;
        public double LightningTop1Threshold { get; init; } = 0.86;
        public string IndexPath { get; init; } = "auto";
        public bool AutoBuildOnStart { get; init; } = true;
        public bool BlockDuringBuild { get; init; } = true;
        public EmbeddingToolsScoreWeights ScoreWeights { get; init; } = new();
        public EmbeddingDynamicThresholds DynamicThresholds { get; init; } = new();
    }

    /// <summary>
    /// 工具向量检索的分数权重。
    /// </summary>
    public sealed class EmbeddingToolsScoreWeights
    {
        public double Name { get; init; } = 0.6;
        public double Description { get; init; } = 0.4;
    }

    /// <summary>
    /// 动态阈值配置（最小实现）。
    /// </summary>
    public sealed class EmbeddingDynamicThresholds
    {
        public bool Enabled { get; init; } = true;
        public double Smoothing { get; init; } = 0.2;
        public double MinTop1 { get; init; } = 0.78;
        public double MaxTop1 { get; init; } = 0.90;
    }

    /// <summary>
    /// 历史/前情提要配置（P10）。仅 Core 内部使用，外部快照不暴露。
    /// </summary>
    public sealed class HistoryConfig
    {
        /// <summary>
        /// 每 N 轮生成一次总结。
        /// </summary>
        public int SummaryEveryNRounds { get; init; } = 5;

        /// <summary>
        /// 每 10 轮叠加/压缩一次前情提要（此处为阈值，通常为 10）。
        /// </summary>
        public int RecapUpdateEveryRounds { get; init; } = 10;

        /// <summary>
        /// 前情提要字典最大条目数。1=仅最新；0/负数=无限。
        /// </summary>
        public int RecapDictMaxEntries { get; init; } = 20;

        /// <summary>
        /// 单条前情提要最大长度（字符）。
        /// </summary>
        public int RecapMaxChars { get; init; } = 1200;

        /// <summary>
        /// 历史 UI 分页大小。
        /// </summary>
        public int HistoryPageSize { get; init; } = 100;

        /// <summary>
        /// 组装提示总长度预算（字符）。
        /// </summary>
        public int MaxPromptChars { get; init; } = 4000;

        /// <summary>
        /// 预算/延迟限制。
        /// </summary>
        public HistoryBudgetConfig Budget { get; init; } = new();
    }

    /// <summary>
    /// 历史相关的预算/时延参数。
    /// </summary>
    public sealed class HistoryBudgetConfig
    {
        /// <summary>
        /// 单次总结/叠加的最大延迟（毫秒）。
        /// </summary>
        public int MaxLatencyMs { get; init; } = 5000;

        /// <summary>
        /// 每月大模型调用预算上限（美元）。本阶段仅作文档展示，不做硬限制。
        /// </summary>
        public double MonthlyBudgetUSD { get; init; } = 5.0;
    }
}