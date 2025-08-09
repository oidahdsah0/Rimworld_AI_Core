namespace RimAI.Core.Contracts.Eventing
{
    /// <summary>
    /// Defines the priority of an event, used by the EventAggregatorService
    /// to determine whether to trigger an immediate LLM call.
    /// </summary>
    public enum EventPriority
    {
        /// <summary>
        /// Low priority events, typically informational.
        /// Will be buffered and aggregated.
        /// </summary>
        Low,

        /// <summary>
        /// Medium priority events that might be of interest.
        /// Will be buffered but might trigger aggregation sooner.
        /// </summary>
        Medium,

        /// <summary>
        /// High priority events that are likely important.
        /// May trigger an immediate aggregation cycle.
        /// </summary>
        High,

        /// <summary>
        /// Critical events that demand immediate attention.
        /// Will bypass buffering and trigger an immediate LLM call.
        /// </summary>
        Critical
    }

    /// <summary>
    /// Represents a single, discrete event that occurs within the game.
    /// This is the base contract for all events handled by the RimAI system.
    /// </summary>
    public interface IEvent
    {
        /// <summary>
        /// A unique identifier for this specific event instance.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// The timestamp when the event occurred.
        /// </summary>
        System.DateTime Timestamp { get; }

        /// <summary>
        /// The priority level of the event.
        /// </summary>
        EventPriority Priority { get; }

        /// <summary>
        /// A concise, human-readable description of the event.
        /// This will be used to construct prompts for the LLM.
        /// </summary>
        /// <returns>A string describing the event.</returns>
        string Describe();
    }

    /// <summary>
    /// 编排进度事件：用于实时反馈 Orchestration/Planner 的阶段进度与细节。
    /// </summary>
    public sealed class OrchestrationProgressEvent : IEvent
    {
        public string Id { get; } = System.Guid.NewGuid().ToString();
        public System.DateTime Timestamp { get; } = System.DateTime.UtcNow;
        public EventPriority Priority { get; } = EventPriority.Low;

        /// <summary>
        /// 事件来源（如 ClassicStrategy / EmbeddingFirstStrategy / Planner）。
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// 阶段标识（如 ToolMatch / RAG / ExecuteTool / Summarize / FinalPrompt）。
        /// </summary>
        public string Stage { get; set; } = string.Empty;

        /// <summary>
        /// 简短描述信息。
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 结构化数据（可选，序列化为 JSON 字符串），便于 UI 展示更多细节。
        /// </summary>
        public string PayloadJson { get; set; } = string.Empty;

        public string Describe() => $"[{Source}] {Stage}: {Message}";
    }
}

