using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Contracts.Eventing;

namespace RimAI.Core.Modules.Eventing
{
    /// <summary>
    /// A simple, thread-safe implementation of the IEventBus interface.
    /// It uses a dictionary to store subscriptions and lock statements
    /// to ensure thread safety during publish/subscribe operations.
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscriptions = new Dictionary<Type, List<Delegate>>();
        private readonly object _lock = new object();

        public void Publish(IEvent @event)
        {
            if (@event == null) return;

            var eventType = @event.GetType();
            List<Delegate> handlersToInvoke = new List<Delegate>();

            lock (_lock)
            {
                // 收集所有订阅了基类 / 接口的处理器。例如：订阅了 IEvent 或具体派生事件。
                foreach (var entry in _subscriptions)
                {
                    if (entry.Key.IsAssignableFrom(eventType))
                    {
                        // 复制到临时列表，避免锁内执行回调造成死锁
                        handlersToInvoke.AddRange(entry.Value);
                    }
                }
            }

            // 去重（同一处理器可能既订阅基类又订阅子类）
            foreach (var handler in handlersToInvoke.Distinct())
            {
                handler.DynamicInvoke(@event);
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                if (!_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType] = new List<Delegate>();
                }
                _subscriptions[eventType].Add(handler);
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                if (_subscriptions.ContainsKey(eventType))
                {
                    _subscriptions[eventType].Remove(handler);
                    if (_subscriptions[eventType].Count == 0)
                    {
                        _subscriptions.Remove(eventType);
                    }
                }
            }
        }
    }
}

