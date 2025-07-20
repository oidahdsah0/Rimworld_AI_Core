using System;
using RimAI.Core.Architecture.Interfaces;

namespace RimAI.Core.Officers.Events
{
    /// <summary>
    /// 总督建议事件 - 当总督提供建议时触发
    /// </summary>
    public class GovernorAdviceEvent : IEvent
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string EventId { get; } = Guid.NewGuid().ToString();

        public string UserQuery { get; set; }
        public string AdviceProvided { get; set; }
        public string ColonyStatus { get; set; }
        public bool WasSuccessful { get; set; }
        
        public GovernorAdviceEvent(string userQuery, string advice, string colonyStatus, bool successful)
        {
            UserQuery = userQuery;
            AdviceProvided = advice;
            ColonyStatus = colonyStatus;
            WasSuccessful = successful;
        }
    }
}
