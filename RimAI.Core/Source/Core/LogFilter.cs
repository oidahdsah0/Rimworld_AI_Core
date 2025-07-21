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
        private static Harmony _harmony;
        
        static LogFilter()
        {
            try
            {
                // 延迟初始化，避免在设置加载前就执行
                _harmony = new Harmony("rimai.core.logfilter");
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
                bool shouldSuppress = SettingsManager.Settings?.Debug?.SuppressGameProfilerLogs ?? false; // 默认不启用，避免问题
                
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
                if (_harmony == null)
                {
                    _harmony = new Harmony("rimai.core.logfilter");
                }
                
                // ✅ 修复：明确指定Log.Message(string)重载，避免歧义
                var logMessageMethod = AccessTools.Method(typeof(Log), nameof(Log.Message), new Type[] { typeof(string) });
                var prefixMethod = AccessTools.Method(typeof(LogFilter), nameof(MessagePrefix));
                
                if (logMessageMethod != null && prefixMethod != null)
                {
                    // 检查是否已经被patch过
                    if (!_isActive)
                    {
                        _harmony.Patch(logMessageMethod, new HarmonyMethod(prefixMethod));
                        _isActive = true;
                        Log.Message("[LogFilter] 🔇 游戏性能日志过滤已启用");
                    }
                }
                else
                {
                    Log.Warning("[LogFilter] 无法找到Log.Message方法或前缀方法");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 启用过滤失败: {ex.Message}");
                // 如果启用失败，确保状态正确
                _isActive = false;
            }
        }
        
        private static void DisableLogFiltering()
        {
            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchAll("rimai.core.logfilter");
                    _isActive = false;
                    Log.Message("[LogFilter] 🔊 游戏性能日志过滤已禁用");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 禁用过滤失败: {ex.Message}");
                // 即使失败也设置状态为false，避免重复尝试
                _isActive = false;
            }
        }
        
        /// <summary>
        /// Log.Message的前缀Hook - 过滤性能监控消息
        /// ✅ 修复：明确方法签名，匹配Log.Message(string text)
        /// </summary>
        public static bool MessagePrefix(string text)
        {
            try
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
            catch (Exception)
            {
                // 如果过滤过程出错，默认允许消息通过
                return true;
            }
        }
        
        /// <summary>
        /// 安全地清理所有patch
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                if (_harmony != null && _isActive)
                {
                    _harmony.UnpatchAll("rimai.core.logfilter");
                    _isActive = false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[LogFilter] 清理失败: {ex.Message}");
            }
        }
    }
}
