using System;
using System.Collections.Generic;
using RimAI.Core.Contracts.Events;
using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 轻量无状态事件总线：线程安全发布/订阅。
    /// </summary>
    public class EventBus : IEventBus
    {
        private readonly List<Action<IEvent>> _handlers = new();
        private readonly object _lock = new();

        public void Publish(IEvent evt)
        {
            Action<IEvent>[] snapshot;
            lock (_lock)
            {
                snapshot = _handlers.ToArray();
            }
            foreach (var h in snapshot)
            {
                try { h(evt); } catch { /* Swallow to avoid cascade */ }
            }
        }

        public void Subscribe(Action<IEvent> handler)
        {
            lock (_lock)
            {
                if (!_handlers.Contains(handler)) _handlers.Add(handler);
            }
        }

        public void Unsubscribe(Action<IEvent> handler)
        {
            lock (_lock)
            {
                _handlers.Remove(handler);
            }
        }
    }
}
