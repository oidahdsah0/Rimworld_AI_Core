using System;
using System.Collections.Generic;
using RimAI.Core.Architecture.Interfaces;
using UnityEngine;
using Verse;

namespace RimAI.Core.Settings
{
    /// <summary>
    /// RimAI Core 设置数据 - 简化版本，参考Framework设计模式
    /// </summary>
    public class CoreSettings : ModSettings
    {
        // Player Settings
        public PlayerSettings Player = new PlayerSettings();

        // Officer settings
        public Dictionary<string, OfficerConfig> OfficerConfigs = new Dictionary<string, OfficerConfig>();
        
        // 提示词设置
        public Dictionary<string, PromptTemplate> CustomPrompts = new Dictionary<string, PromptTemplate>();
        
        // UI设置
        public UISettings UI = new UISettings();
        
        // 性能设置
        public PerformanceSettings Performance = new PerformanceSettings();
        
        // 缓存设置
        public CacheSettings Cache = new CacheSettings();
        
        // 事件设置
        public EventSettings Events = new EventSettings();
        
        // 调试设置
        public DebugSettings Debug = new DebugSettings();

        // AI Pilot Settings - New section for our dispatcher logic
        public AIPilotSettings AIPilot = new AIPilotSettings();

        public override void ExposeData()
        {
            base.ExposeData();
            try
            {
                // Serialize PlayerSettings
                Scribe_Deep.Look(ref Player, "player");

                // 🎯 简化序列化，避免复杂的深度序列化
                Scribe_Collections.Look(ref OfficerConfigs, "officerConfigs", LookMode.Value, LookMode.Deep);
                Scribe_Collections.Look(ref CustomPrompts, "customPrompts", LookMode.Value, LookMode.Deep);
                
                // 使用安全的Deep序列化
                Scribe_Deep.Look(ref UI, "ui");
                Scribe_Deep.Look(ref Performance, "performance");  
                Scribe_Deep.Look(ref Cache, "cache");
                Scribe_Deep.Look(ref Events, "events");
                Scribe_Deep.Look(ref Debug, "debug");
                Scribe_Deep.Look(ref AIPilot, "aiPilot"); // Serialize the new settings

                // 确保非空 - 使用简单的空检查
                PostLoadValidation();
            }
            catch (Exception ex)
            {
                Log.Error($"[CoreSettings] 序列化失败: {ex.Message}");
                // 发生错误时重置为安全的默认值
                InitializeDefaults();
            }
        }

        /// <summary>
        /// 加载后验证和初始化
        /// </summary>
        private void PostLoadValidation()
        {
            if (Player == null) Player = new PlayerSettings();
            if (OfficerConfigs == null) OfficerConfigs = new Dictionary<string, OfficerConfig>();
            if (CustomPrompts == null) CustomPrompts = new Dictionary<string, PromptTemplate>();
            if (UI == null) UI = new UISettings();
            if (Performance == null) Performance = new PerformanceSettings();
            if (Cache == null) Cache = new CacheSettings();
            if (Events == null) Events = new EventSettings();
            if (Debug == null) Debug = new DebugSettings();
            if (AIPilot == null) AIPilot = new AIPilotSettings(); // Validate the new settings
        }

        /// <summary>
        /// 初始化默认设置
        /// </summary>
        private void InitializeDefaults()
        {
            Player = new PlayerSettings();
            OfficerConfigs = new Dictionary<string, OfficerConfig>();
            CustomPrompts = new Dictionary<string, PromptTemplate>();
            UI = new UISettings();
            Performance = new PerformanceSettings();
            Cache = new CacheSettings();
            Events = new EventSettings();
            Debug = new DebugSettings();
            AIPilot = new AIPilotSettings(); // Reset the new settings
            
            Log.Message("[CoreSettings] 已初始化为默认设置");
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        public void ResetToDefaults()
        {
            InitializeDefaults();
            Log.Message("[CoreSettings] 设置已重置为默认值");
        }

        /// <summary>
        /// 获取或创建官员配置
        /// </summary>
        public OfficerConfig GetOfficerConfig(string officerName)
        {
            if (string.IsNullOrEmpty(officerName)) return new OfficerConfig();
            
            if (!OfficerConfigs.TryGetValue(officerName, out var config))
            {
                config = new OfficerConfig { Name = officerName };
                OfficerConfigs[officerName] = config;
            }
            return config;
        }
    }

    /// <summary>
    /// 官员配置
    /// </summary>
    public class OfficerConfig : IExposable
    {
        public string Name = "";
        public bool IsEnabled = true;
        public float ResponseTemperature = 0.7f;
        public bool PreferStreaming = false;
        public int CacheDurationMinutes = 5;
        public bool AutoAnalysis = true;
        public Dictionary<string, string> CustomTemplateIds = new Dictionary<string, string>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref Name, "name", "");
            Scribe_Values.Look(ref IsEnabled, "isEnabled", true);
            Scribe_Values.Look(ref ResponseTemperature, "responseTemperature", 0.7f);
            Scribe_Values.Look(ref PreferStreaming, "preferStreaming", false);
            Scribe_Values.Look(ref CacheDurationMinutes, "cacheDurationMinutes", 5);
            Scribe_Values.Look(ref AutoAnalysis, "autoAnalysis", true);
            Scribe_Collections.Look(ref CustomTemplateIds, "customTemplateIds", LookMode.Value, LookMode.Value);
            
            if (CustomTemplateIds == null) CustomTemplateIds = new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// UI设置
    /// </summary>
    public class UISettings : IExposable
    {
        public bool ShowAdvancedOptions = false;
        public bool ShowDebugInfo = false;
        public bool ShowPerformanceStats = false;
        public bool EnableNotifications = true;
        public bool EnableStreamingIndicator = true;
        public float WindowOpacity = 0.95f;
        public int MaxDisplayedMessages = 50;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ShowAdvancedOptions, "showAdvancedOptions", false);
            Scribe_Values.Look(ref ShowDebugInfo, "showDebugInfo", false);
            Scribe_Values.Look(ref ShowPerformanceStats, "showPerformanceStats", false);
            Scribe_Values.Look(ref EnableNotifications, "enableNotifications", true);
            Scribe_Values.Look(ref EnableStreamingIndicator, "enableStreamingIndicator", true);
            Scribe_Values.Look(ref WindowOpacity, "windowOpacity", 0.95f);
            Scribe_Values.Look(ref MaxDisplayedMessages, "maxDisplayedMessages", 50);
        }
    }

    /// <summary>
    /// 性能设置
    /// </summary>
    public class PerformanceSettings : IExposable
    {
        public int MaxConcurrentRequests = 3;
        public int RequestTimeoutSeconds = 30;
        public bool EnableBatchProcessing = true;
        public int AnalysisIntervalTicks = 2500; // 约1游戏小时
        public bool EnableBackgroundAnalysis = true;
        public int MaxBackgroundTasks = 2;
        public bool EnableMemoryMonitoring = false;

        public void ExposeData()
        {
            Scribe_Values.Look(ref MaxConcurrentRequests, "maxConcurrentRequests", 3);
            Scribe_Values.Look(ref RequestTimeoutSeconds, "requestTimeoutSeconds", 30);
            Scribe_Values.Look(ref EnableBatchProcessing, "enableBatchProcessing", true);
            Scribe_Values.Look(ref AnalysisIntervalTicks, "analysisIntervalTicks", 2500);
            Scribe_Values.Look(ref EnableBackgroundAnalysis, "enableBackgroundAnalysis", true);
            Scribe_Values.Look(ref MaxBackgroundTasks, "maxBackgroundTasks", 2);
            Scribe_Values.Look(ref EnableMemoryMonitoring, "enableMemoryMonitoring", false);
        }
    }

    /// <summary>
    /// 缓存设置 - 与Framework配置系统保持一致，适应现代硬件
    /// </summary>
    public class CacheSettings : IExposable
    {
        public bool EnableCaching = true;
        public int MaxCacheEntries = 500; // 提高默认值，适应现代硬件
        public int DefaultCacheDurationMinutes = 30; // 增加默认值，减少API调用
        public int CleanupIntervalMinutes = 2; // 适中的清理频率
        public bool EnableSmartCaching = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableCaching, "enableCaching", true);
            Scribe_Values.Look(ref MaxCacheEntries, "maxCacheEntries", 500); // 更新默认值
            Scribe_Values.Look(ref DefaultCacheDurationMinutes, "defaultCacheDurationMinutes", 30); // 更新默认值
            Scribe_Values.Look(ref CleanupIntervalMinutes, "cleanupIntervalMinutes", 2); // 更新默认值
            Scribe_Values.Look(ref EnableSmartCaching, "enableSmartCaching", true);
        }
    }

    /// <summary>
    /// 事件设置
    /// </summary>
    public class EventSettings : IExposable
    {
        public bool EnableEventBus = true;
        public bool EnableAutoThreatDetection = true;
        public bool EnableAutoResourceMonitoring = true;
        public bool EnableAutoColonistMonitoring = false;
        public int EventProcessingDelayMs = 100;
        public bool ShowEventNotifications = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableEventBus, "enableEventBus", true);
            Scribe_Values.Look(ref EnableAutoThreatDetection, "enableAutoThreatDetection", true);
            Scribe_Values.Look(ref EnableAutoResourceMonitoring, "enableAutoResourceMonitoring", true);
            Scribe_Values.Look(ref EnableAutoColonistMonitoring, "enableAutoColonistMonitoring", false);
            Scribe_Values.Look(ref EventProcessingDelayMs, "eventProcessingDelayMs", 100);
            Scribe_Values.Look(ref ShowEventNotifications, "showEventNotifications", true);
        }
    }

    /// <summary>
    /// 调试设置
    /// </summary>
    public class DebugSettings : IExposable
    {
        public bool EnableVerboseLogging = false;
        public bool EnablePerformanceProfiling = false;
        public bool SaveAnalysisResults = false;
        public bool ShowInternalEvents = false;
        public bool SuppressGameProfilerLogs = true; // 🔇 抑制游戏性能分析日志

        public void ExposeData()
        {
            Scribe_Values.Look(ref EnableVerboseLogging, "enableVerboseLogging", false);
            Scribe_Values.Look(ref EnablePerformanceProfiling, "enablePerformanceProfiling", false);
            Scribe_Values.Look(ref SaveAnalysisResults, "saveAnalysisResults", false);
            Scribe_Values.Look(ref ShowInternalEvents, "showInternalEvents", false);
            Scribe_Values.Look(ref SuppressGameProfilerLogs, "suppressGameProfilerLogs", true);
        }
    }

    /// <summary>
    /// AI Pilot Settings - Contains settings related to the core AI decision-making process.
    /// </summary>
    public class AIPilotSettings : IExposable
    {
        public DispatchMode DispatcherMode = DispatchMode.LlmTool;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DispatcherMode, "dispatcherMode", DispatchMode.LlmTool);
        }
    }

    /// <summary>
    /// Defines the different strategies the AI can use to select a tool.
    /// </summary>
    public enum DispatchMode
    {
        /// <summary>
        /// (Recommended) Use the LLM's native Tool Calling feature. Most reliable.
        /// </summary>
        LlmTool,
        /// <summary>
        /// (Fallback) Force the LLM to output a specific JSON format.
        /// </summary>
        LlmJson,
        /// <summary>
        /// (Experimental) Use a local embedding model for ultra-fast, offline dispatching.
        /// </summary>
        LocalEmbedding
    }

    public class PlayerSettings : IExposable
    {
        public string Nickname = "指挥官";

        public void ExposeData()
        {
            Scribe_Values.Look(ref Nickname, "nickname", "指挥官");
        }
    }

    /// <summary>
    /// 设置管理器 - 简化版本，避免循环引用
    /// </summary>
    public static class SettingsManager
    {
        private static CoreSettings _settings;
        
        public static CoreSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    try
                    {
                        // 🎯 修复崩溃：更安全的获取方式，避免循环引用
                        var mod = LoadedModManager.GetMod<RimAICoreMod>();
                        if (mod != null)
                        {
                            _settings = mod.GetSettings<CoreSettings>();
                        }
                        else
                        {
                            Log.Warning("[SettingsManager] RimAICoreMod未找到，创建默认设置");
                            _settings = new CoreSettings();
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Log.Error($"[SettingsManager] 获取设置失败: {ex.Message}");
                        _settings = new CoreSettings();
                    }
                }
                return _settings;
            }
        }

        /// <summary>
        /// 设置设置实例（供 RimAICoreMod 直接调用）
        /// </summary>
        public static void SetSettings(CoreSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        public static void SaveSettings()
        {
            try
            {
                var mod = LoadedModManager.GetMod<RimAICoreMod>();
                mod?.WriteSettings();
                Log.Message("[SettingsManager] 设置保存成功");
            }
            catch (Exception ex)
            {
                Log.Error($"[SettingsManager] 设置保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取官员配置
        /// </summary>
        public static OfficerConfig GetOfficerConfig(string officerName)
        {
            return Settings.GetOfficerConfig(officerName);
        }

        /// <summary>
        /// 应用设置到服务 - 简化版本，避免在设置加载时调用服务
        /// </summary>
        public static void ApplySettings()
        {
            try
            {
                // 🎯 这里暂时不调用具体服务，避免循环引用
                // 服务将在需要时主动获取最新设置
                Log.Message("[SettingsManager] 设置更改信号已发送");
            }
            catch (Exception ex)
            {
                Log.Error($"[SettingsManager] 应用设置失败: {ex.Message}");
            }
        }
    }
}
