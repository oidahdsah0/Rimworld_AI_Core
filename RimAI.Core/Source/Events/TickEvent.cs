using System;
using RimAI.Core.Contracts.Events;

namespace RimAI.Core.Events
{
    /// <summary>
    /// 每 N 游戏刻触发的测试事件。
    /// </summary>
    public class TickEvent : IEvent
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; } = DateTime.UtcNow;
        public EventPriority Priority => EventPriority.Low;
        public int GameTick { get; }
        public TickEvent(int tick) => GameTick = tick;
        public string Describe() => $"TickEvent tick={GameTick}";
    }
}
