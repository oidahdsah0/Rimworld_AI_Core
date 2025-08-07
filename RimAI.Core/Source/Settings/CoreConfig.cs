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
}