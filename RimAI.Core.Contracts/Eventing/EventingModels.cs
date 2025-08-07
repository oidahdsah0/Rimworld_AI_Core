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
}

