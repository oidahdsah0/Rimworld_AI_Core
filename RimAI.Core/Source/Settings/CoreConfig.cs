namespace RimAI.Core.Settings
{
    /// <summary>
    /// 顶级配置对象，包含所有子配置。属性设为 init 只读，以保证不可变性。
    /// </summary>
    public sealed class CoreConfig
    {
        public LLMConfig LLM { get; init; } = new();
        public CacheConfig Cache { get; init; } = new();
        public EventAggregatorConfig EventAggregator { get; init; } = new();
        public OrchestrationConfig Orchestration { get; init; } = new();
        public EmbeddingConfig Embedding { get; init; } = new();

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

    /// <summary>
    /// 缓存配置。
    /// </summary>
    public sealed class CacheConfig
    {
        /// <summary>
        /// 默认缓存过期时间（分钟）。
        /// </summary>
        public int DefaultExpirationMinutes { get; init; } = 5;
    }

    /// <summary>
    /// 编排层配置。
    /// </summary>
    public sealed class OrchestrationConfig
    {
        /// <summary>
        /// 策略选择：Classic | EmbeddingFirst。
        /// </summary>
        public string Strategy { get; init; } = "Classic";
    }

    /// <summary>
    /// Embedding/RAG 相关配置（最小）。
    /// </summary>
    public sealed class EmbeddingConfig
    {
        public bool Enabled { get; init; } = true;
        public int TopK { get; init; } = 5;
        public int CacheMinutes { get; init; } = 60;
        public int MaxContextChars { get; init; } = 2000;
        public EmbeddingToolsConfig Tools { get; init; } = new();
    }

    /// <summary>
    /// 工具向量库与匹配模式配置（预留，S2 仅占位）。
    /// </summary>
    public sealed class EmbeddingToolsConfig
    {
        public string Mode { get; init; } = "Classic"; // S2 暂不启用
        public double Top1Threshold { get; init; } = 0.82;
        public double LightningTop1Threshold { get; init; } = 0.86;
        public string IndexPath { get; init; } = "auto";
        public bool AutoBuildOnStart { get; init; } = true;
        public bool BlockDuringBuild { get; init; } = true;
    }
}