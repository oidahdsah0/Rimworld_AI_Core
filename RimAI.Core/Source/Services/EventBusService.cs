using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 事件总线实现 - 负责事件的发布和订阅
    /// </summary>
    public class EventBusService : IEventBus
    {
        private static EventBusService _instance;
        public static EventBusService Instance => _instance ??= new EventBusService();

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
                    Log.Message($"[EventBus] Subscribed handler {handler.GetType().Name} to event {eventType.Name}");
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
                    Log.Message($"[EventBus] Unsubscribed handler {handler.GetType().Name} from event {eventType.Name}");
                    
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
                if (!_handlers.TryGetValue(eventType, out handlers) || handlers.Count == 0)
                {
                    return;
                }
                
                // 创建副本避免并发修改
                handlers = new List<IEventHandler>(handlers);
            }

            Log.Message($"[EventBus] Publishing event {eventType.Name} to {handlers.Count} handlers");

            var tasks = new List<Task>();
            
            foreach (var handler in handlers)
            {
                var task = HandleEventSafely(handler, eventData, cancellationToken);
                tasks.Add(task);
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                Log.Error($"[EventBus] Error publishing event {eventType.Name}: {ex}");
            }
        }

        private async Task HandleEventSafely(IEventHandler handler, IEvent eventData, CancellationToken cancellationToken)
        {
            try
            {
                await handler.HandleAsync(eventData, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 取消操作是正常的，不记录错误
            }
            catch (Exception ex)
            {
                Log.Error($"[EventBus] Handler {handler.GetType().Name} failed to process event {eventData.GetType().Name}: {ex}");
            }
        }

        /// <summary>
        /// 获取指定事件类型的处理器数量
        /// </summary>
        public int GetHandlerCount<TEvent>() where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            lock (_lock)
            {
                return _handlers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
            }
        }

        /// <summary>
        /// 清除所有处理器（主要用于测试）
        /// </summary>
        public void ClearAllHandlers()
        {
            lock (_lock)
            {
                _handlers.Clear();
                Log.Message("[EventBus] All handlers cleared");
            }
        }

        /// <summary>
        /// 获取系统状态信息
        /// </summary>
        public string GetStatusInfo()
        {
            lock (_lock)
            {
                var totalHandlers = 0;
                foreach (var handlers in _handlers.Values)
                {
                    totalHandlers += handlers.Count;
                }

                return $"事件总线状态: {_handlers.Count} 种事件类型, {totalHandlers} 个处理器";
            }
        }
    }
}
