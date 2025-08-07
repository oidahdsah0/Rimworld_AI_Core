namespace RimAI.Core.Contracts.Eventing
{
    /// <summary>
    /// A high-level service responsible for intelligently processing,
    /// aggregating, and throttling events before they are sent to the LLM.
    /// It acts as a smart filter to prevent event spam and reduce API costs.
    /// </summary>
    public interface IEventAggregatorService
    {
        /// <summary>
        /// Initializes the service, setting up timers and subscribing to the event bus.
        /// This should be called once during the application's startup sequence.
        /// </summary>
        void Initialize();
    }
}

