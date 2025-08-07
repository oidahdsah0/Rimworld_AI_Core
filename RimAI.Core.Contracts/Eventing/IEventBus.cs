using System;

namespace RimAI.Core.Contracts.Eventing
{
    /// <summary>
    /// Provides a lightweight, in-memory message bus for publishing
    /// and subscribing to IEvent instances. This is a low-level
    /// service responsible only for event dispatching.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        /// <param name="event">The event to publish.</param>
        void Publish(IEvent @event);

        /// <summary>
        /// Subscribes to receive events of a specific type.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to.</typeparam>
        /// <param name="handler">The action to execute when the event is published.</param>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

        /// <summary>
        /// Unsubscribes an action from a specific event type.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to unsubscribe from.</typeparam>
        /// <param name="handler">The action to remove.</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
    }
}

