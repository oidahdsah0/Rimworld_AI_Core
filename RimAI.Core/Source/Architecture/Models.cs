using System;
using System.Collections.Generic;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// 官员角色枚举
    /// </summary>
    public enum OfficerRole
    {
        Governor,      // 总督 - 整体管理
        Military,      // 军事 - 防务和战斗
        Logistics,     // 后勤 - 资源和建设
        Medical,       // 医疗 - 健康和医疗
        Research,      // 科研 - 研究和技术
        Diplomat,      // 外交 - 对外关系
        Security,      // 安全 - 内部秩序
        Economy        // 经济 - 贸易和财务
    }

    /// <summary>
    /// 殖民地状态数据
    /// </summary>
    public class ColonyStatus
    {
        public int ColonistCount { get; set; }
        public string ResourceSummary { get; set; }
        public ThreatLevel ThreatLevel { get; set; }
        public List<string> ActiveEvents { get; set; } = new List<string>();
        public string WeatherCondition { get; set; }
        public string Season { get; set; }
        public Dictionary<string, float> ResourceLevels { get; set; } = new Dictionary<string, float>();
        public List<ColonistInfo> Colonists { get; set; } = new List<ColonistInfo>();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 威胁等级
    /// </summary>
    public enum ThreatLevel
    {
        None,      // 无威胁
        Low,       // 低威胁
        Medium,    // 中等威胁
        High,      // 高威胁
        Critical   // 危急威胁
    }

    /// <summary>
    /// 威胁信息
    /// </summary>
    public class ThreatInfo
    {
        public string Type { get; set; }
        public ThreatLevel Level { get; set; }
        public string Description { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 资源报告
    /// </summary>
    public class ResourceReport
    {
        public Dictionary<string, ResourceStatus> Resources { get; set; } = new Dictionary<string, ResourceStatus>();
        public List<string> CriticalShortages { get; set; } = new List<string>();
        public List<string> Surpluses { get; set; } = new List<string>();
        public string OverallStatus { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 资源状态
    /// </summary>
    public class ResourceStatus
    {
        public string Name { get; set; }
        public float Current { get; set; }
        public float Maximum { get; set; }
        public float DailyChange { get; set; }
        public ResourcePriority Priority { get; set; }
        public string Status { get; set; }
    }

    /// <summary>
    /// 资源优先级
    /// </summary>
    public enum ResourcePriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// 殖民者信息
    /// </summary>
    public class ColonistInfo
    {
        public string Name { get; set; }
        public string Profession { get; set; }
        public List<string> Skills { get; set; } = new List<string>();
        public string HealthStatus { get; set; }
        public string MoodStatus { get; set; }
        public string CurrentTask { get; set; }
        public bool IsAvailable { get; set; }
    }

    /// <summary>
    /// 提示词模板
    /// </summary>
    public class PromptTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Template { get; set; }
        public PromptConstraints Constraints { get; set; } = new PromptConstraints();
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 提示词约束
    /// </summary>
    public class PromptConstraints
    {
        public int? MaxTokens { get; set; }
        public float? Temperature { get; set; }
        public List<string> SafetyRules { get; set; } = new List<string>();
        public string ResponseFormat { get; set; } = "text"; // "text", "json", "list"
        public bool RequireStreaming { get; set; } = false;
        public TimeSpan? Timeout { get; set; }
    }
}
