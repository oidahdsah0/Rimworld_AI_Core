namespace RimAI.Core.Contracts.Events
{
    public enum EventPriority { Low, Medium, High, Critical }

    /// <summary>
    /// 基本游戏事件契约。
    /// </summary>
    public interface IEvent
    {
        string Id { get; }
        System.DateTime Timestamp { get; }
        EventPriority Priority { get; }
        string Describe();
    }
}
