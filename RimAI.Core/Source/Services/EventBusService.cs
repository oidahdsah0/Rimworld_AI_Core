using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services
{
    public class EventBusService : IEventBus
    {
        private readonly ConcurrentDictionary<Type, List<IEventHandler>> _handlers;
        private readonly object _lock = new object();

        public EventBusService()
        {
            _handlers = new ConcurrentDictionary<Type, List<IEventHandler>>();
        }

        public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null) return;

            var eventType = typeof(TEvent);
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers = new List<IEventHandler>();
                    _handlers[eventType] = handlers;
                }

                if (!handlers.Contains(handler))
                {
                    handlers.Add(handler);
                }
            }
        }

        public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
        {
            if (handler == null) return;

            var eventType = typeof(TEvent);
            
            lock (_lock)
            {
                if (_handlers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);
                    
                    if (handlers.Count == 0)
                    {
                        _handlers.TryRemove(eventType, out _);
                    }
                }
            }
        }

        public async Task PublishAsync<TEvent>(TEvent eventData, CancellationToken cancellationToken = default) where TEvent : IEvent
        {
            if (eventData == null) return;

            var eventType = typeof(TEvent);
            List<IEventHandler> handlers;
            
            lock (_lock)
            {
                if (!_handlers.TryGetValue(eventType, out var eventHandlers) || eventHandlers.Count == 0)
                {
                    return;
                }
                
                handlers = new List<IEventHandler>(eventHandlers); // Create a copy
            }

            var tasks = new List<Task>();
            foreach (var handler in handlers)
            {
                if (cancellationToken.IsCancellationRequested)
            {
                    break;
            }

                if (handler is IEventHandler<TEvent> specificHandler)
        {
                    tasks.Add(Task.Run(() => specificHandler.HandleAsync(eventData, cancellationToken), cancellationToken));
            }
        }

            await Task.WhenAll(tasks);
                }
    }
            }

