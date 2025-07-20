using System;
using System.Reflection;
using Verse;
using HarmonyLib;
using RimAI.Core.Settings;

namespace RimAI.Core
{
    /// <summary>
    /// 日志过滤器 - 减少性能监控日志噪音
    /// </summary>
    [StaticConstructorOnStartup]
    public static class LogFilter
    {
        private static bool _isActive = false;
        
        static LogFilter()
        {
            try
            {
                // 在设置系统加载后激活
                ApplyFiltersIfNeeded();
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 初始化失败: {ex.Message}");
            }
        }
        
        public static void ApplyFiltersIfNeeded()
        {
            try
            {
                bool shouldSuppress = SettingsManager.Settings?.Debug?.SuppressGameProfilerLogs ?? true;
                
                if (shouldSuppress && !_isActive)
                {
                    EnableLogFiltering();
                }
                else if (!shouldSuppress && _isActive)
                {
                    DisableLogFiltering();
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 应用过滤设置失败: {ex.Message}");
            }
        }
        
        private static void EnableLogFiltering()
        {
            try
            {
                var harmony = new Harmony("rimai.core.logfilter");
                
                // Hook Log.Message 来过滤性能监控消息
                var logMessageMethod = AccessTools.Method(typeof(Log), nameof(Log.Message));
                var prefixMethod = AccessTools.Method(typeof(LogFilter), nameof(MessagePrefix));
                
                if (logMessageMethod != null && prefixMethod != null)
                {
                    harmony.Patch(logMessageMethod, new HarmonyMethod(prefixMethod));
                    _isActive = true;
                    Log.Message("[LogFilter] 🔇 游戏性能日志过滤已启用");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 启用过滤失败: {ex.Message}");
            }
        }
        
        private static void DisableLogFiltering()
        {
            try
            {
                var harmony = new Harmony("rimai.core.logfilter");
                harmony.UnpatchAll("rimai.core.logfilter");
                _isActive = false;
                Log.Message("[LogFilter] 🔊 游戏性能日志过滤已禁用");
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 禁用过滤失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Log.Message的前缀Hook - 过滤性能监控消息
        /// </summary>
        public static bool MessagePrefix(string text)
        {
            // 过滤常见的性能监控日志
            if (string.IsNullOrEmpty(text)) return true;
            
            // 检查是否是性能分析相关的消息
            if (text.Contains("DeepProfiler") ||
                text.Contains("ThreadLocalDeepProfiler") ||
                text.Contains("Steam geyser erupted") ||
                text.Contains("Sound finished") ||
                (text.Contains("Analysis") && text.Contains("ms")) ||
                text.Contains("Profiler"))
            {
                // 被过滤的消息不输出
                return false;
            }
            
            // 其他消息正常输出
            return true;
        }
    }
}
