using System;
using RimAI.Core.Architecture.Interfaces;

namespace RimAI.Core.Architecture.Events
{
    /// <summary>
    /// 基础事件实现
    /// </summary>
    public abstract class BaseEvent : IEvent
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string EventId { get; } = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// 威胁检测事件
    /// </summary>
    public class ThreatDetectedEvent : BaseEvent
    {
        public ThreatInfo Threat { get; set; }
        public string Source { get; set; } // 威胁来源

        public ThreatDetectedEvent(ThreatInfo threat, string source = null)
        {
            Threat = threat;
            Source = source;
        }
    }

    /// <summary>
    /// 资源危机事件
    /// </summary>
    public class ResourceCrisisEvent : BaseEvent
    {
        public string ResourceType { get; set; }
        public float CurrentLevel { get; set; }
        public float CriticalLevel { get; set; }
        public string Severity { get; set; }

        public ResourceCrisisEvent(string resourceType, float currentLevel, float criticalLevel, string severity)
        {
            ResourceType = resourceType;
            CurrentLevel = currentLevel;
            CriticalLevel = criticalLevel;
            Severity = severity;
        }
    }

    /// <summary>
    /// 殖民者状态变化事件
    /// </summary>
    public class ColonistStatusChangedEvent : BaseEvent
    {
        public string ColonistName { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string StatusType { get; set; } // "health", "mood", "task"

        public ColonistStatusChangedEvent(string colonistName, string oldStatus, string newStatus, string statusType)
        {
            ColonistName = colonistName;
            OldStatus = oldStatus;
            NewStatus = newStatus;
            StatusType = statusType;
        }
    }

    /// <summary>
    /// 重要决策需求事件
    /// </summary>
    public class DecisionRequiredEvent : BaseEvent
    {
        public string Situation { get; set; }
        public string Context { get; set; }
        public ThreatLevel Urgency { get; set; }
        public OfficerRole RecommendedOfficer { get; set; }

        public DecisionRequiredEvent(string situation, string context, ThreatLevel urgency, OfficerRole recommendedOfficer = OfficerRole.Governor)
        {
            Situation = situation;
            Context = context;
            Urgency = urgency;
            RecommendedOfficer = recommendedOfficer;
        }
    }

    /// <summary>
    /// AI响应完成事件
    /// </summary>
    public class AIResponseCompletedEvent : BaseEvent
    {
        public string OfficerName { get; set; }
        public string Request { get; set; }
        public string Response { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public bool IsSuccess { get; set; }

        public AIResponseCompletedEvent(string officerName, string request, string response, TimeSpan processingTime, bool isSuccess)
        {
            OfficerName = officerName;
            Request = request;
            Response = response;
            ProcessingTime = processingTime;
            IsSuccess = isSuccess;
        }
    }

    /// <summary>
    /// 系统配置变更事件
    /// </summary>
    public class ConfigurationChangedEvent : BaseEvent
    {
        public string ConfigKey { get; set; }
        public object OldValue { get; set; }
        public object NewValue { get; set; }
        public string Source { get; set; } // 变更来源

        public ConfigurationChangedEvent(string configKey, object oldValue, object newValue, string source)
        {
            ConfigKey = configKey;
            OldValue = oldValue;
            NewValue = newValue;
            Source = source;
        }
    }
}
