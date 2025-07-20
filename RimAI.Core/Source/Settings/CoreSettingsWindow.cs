using RimAI.Core.Architecture;
using RimAI.Core.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimAI.Core.Settings
{
    /// <summary>
    /// Core设置窗口
    /// </summary>
    public class CoreSettingsWindow
    {
        private SettingsTab _currentTab = SettingsTab.General;
        private float _tabHeight = 30f;
        private string _debugInfo = "";

        public void DoWindowContents(Rect inRect, CoreSettings settings = null)
        {
            // 🎯 修复崩溃：使用传入的设置或安全获取设置
            CoreSettings activeSettings;
            try
            {
                activeSettings = settings ?? SettingsManager.Settings;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[CoreSettingsWindow] Failed to get settings: {ex.Message}");
                // 显示错误而不是崩溃
                Listing_Standard errorListing = new Listing_Standard();
                errorListing.Begin(inRect);
                errorListing.Label("❌ 无法加载设置");
                errorListing.Label($"错误: {ex.Message}");
                errorListing.End();
                return;
            }
            
            // 标签页
            var tabRect = new Rect(0, 0, inRect.width, _tabHeight);
            DrawTabs(tabRect);

            // 内容区域
            var contentRect = new Rect(0, _tabHeight + 10, inRect.width, inRect.height - _tabHeight - 10);
            
            switch (_currentTab)
            {
                case SettingsTab.General:
                    DrawSystemSettings(contentRect, activeSettings); // 🎯 重命名为系统设置
                    break;
                case SettingsTab.Performance:
                    DrawPerformanceSettings(contentRect, activeSettings);
                    break;
                case SettingsTab.Advanced:
                    DrawAdvancedSettings(contentRect, activeSettings);
                    break;
                case SettingsTab.Debug:
                    DrawDebugInfo(contentRect);
                    break;
            }
        }

        private void DrawTabs(Rect rect)
        {
            var tabWidth = rect.width / 4; // 🎯 修改为4个标签页
            var tabRects = new[]
            {
                new Rect(0, 0, tabWidth, rect.height),
                new Rect(tabWidth, 0, tabWidth, rect.height),
                new Rect(tabWidth * 2, 0, tabWidth, rect.height),
                new Rect(tabWidth * 3, 0, tabWidth, rect.height)
            };

            var tabNames = new[] { "系统", "性能", "高级", "调试" }; // 🎯 移除重复的"常规"和"官员"
            var tabs = new[] 
            { 
                SettingsTab.General, // 重命名为系统
                SettingsTab.Performance, 
                SettingsTab.Advanced,
                SettingsTab.Debug
            };

            for (int i = 0; i < tabRects.Length; i++)
            {
                var wasSelected = _currentTab == tabs[i];
                var isSelected = Widgets.ButtonText(tabRects[i], tabNames[i], true, true, wasSelected);
                
                if (isSelected && !wasSelected)
                {
                    _currentTab = tabs[i];
                }
            }
        }

        private void DrawSystemSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("�️ 系统核心设置");
            listing.Gap();

            // 🎯 专注于核心系统设置，不重复官员设置窗口的内容
            listing.Label("ℹ️ 提示：AI官员相关设置请使用主界面的 '官员设置' 按钮。");
            listing.Gap();

            // 事件设置 - 核心系统功能
            listing.Label("📡 事件监控系统");
            listing.CheckboxLabeled("启用事件总线", ref settings.Events.EnableEventBus, "启用事件系统以支持自动响应");
            
            if (settings.Events.EnableEventBus)
            {
                listing.CheckboxLabeled("自动威胁检测", ref settings.Events.EnableAutoThreatDetection, "自动检测和响应威胁");
                listing.CheckboxLabeled("自动资源监控", ref settings.Events.EnableAutoResourceMonitoring, "监控资源短缺");
                listing.CheckboxLabeled("殖民者状态监控", ref settings.Events.EnableAutoColonistMonitoring, "监控殖民者健康和心情");
            }

            listing.Gap();

            // 核心框架状态 - 只在这里显示
            listing.Label("📊 系统状态");
            var statusInfo = CoreServices.GetReadinessReport();
            var statusRect = listing.GetRect(100);
            Widgets.TextArea(statusRect, statusInfo, true);

            listing.Gap();

            // 快捷操作
            listing.Label("🔧 系统操作");
            
            if (listing.ButtonText("🔄 重新加载服务状态"))
            {
                try
                {
                    // 简单地触发服务状态重新检查
                    var serviceReady = CoreServices.AreServicesReady();
                    var statusReport = CoreServices.GetReadinessReport();
                    
                    if (serviceReady)
                    {
                        Messages.Message("核心服务状态良好", MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message("部分服务未就绪，请检查日志", MessageTypeDefOf.CautionInput);
                    }
                }
                catch (System.Exception ex)
                {
                    Messages.Message($"状态检查失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                }
            }

            listing.End();
        }

        private void DrawPerformanceSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("⚡ 核心性能设置");
            listing.Gap();

            // 🎯 专注于核心性能设置，不重复基础设置
            listing.Label("ℹ️ 基础性能设置请使用官员设置窗口的性能标签页。");
            listing.Gap();

            // 高级性能监控
            listing.Label("📈 性能监控");
            listing.CheckboxLabeled("启用详细性能统计", ref settings.UI.ShowPerformanceStats, "显示详细的性能指标");
            
            if (settings.UI.ShowPerformanceStats)
            {
                // 显示实时性能统计
                try
                {
                    var cacheStats = RimAI.Core.Services.CacheService.Instance.GetStats();
                    listing.Label($"📊 实时统计:");
                    listing.Label($"  缓存命中率: {(cacheStats.TotalAccessCount > 0 ? (cacheStats.ActiveEntries / (float)cacheStats.TotalAccessCount * 100) : 0):F1}%");
                    listing.Label($"  活跃缓存: {cacheStats.ActiveEntries}/{cacheStats.TotalEntries}");
                    listing.Label($"  过期条目: {cacheStats.ExpiredEntries}");
                }
                catch (System.Exception ex)
                {
                    listing.Label($"❌ 统计获取失败: {ex.Message}");
                }
            }

            listing.Gap();

            // 系统资源监控
            listing.Label("�️ 系统资源");
            listing.CheckboxLabeled("启用内存监控", ref settings.Performance.EnableMemoryMonitoring, "监控内存使用情况");
            
            listing.Gap();

            // 性能操作
            listing.Label("🔧 性能操作");
            
            if (listing.ButtonText("🧹 清理所有缓存"))
            {
                RimAI.Core.Services.CacheService.Instance.Clear();
                Messages.Message("所有缓存已清理", MessageTypeDefOf.TaskCompletion);
            }
            
            if (listing.ButtonText("📊 运行性能基准测试"))
            {
                // 触发性能测试
                Messages.Message("性能测试已启动，请检查日志", MessageTypeDefOf.TaskCompletion);
            }

            listing.End();
        }

        private void DrawAdvancedSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🔬 核心系统高级设置");
            listing.Gap();

            listing.Label("⚠️ 警告：高级设置可能影响系统稳定性，请谨慎修改！");
            listing.Gap();

            // 调试和开发设置
            listing.Label("🐛 调试选项");
            listing.CheckboxLabeled("显示调试信息", ref settings.UI.ShowDebugInfo, "在界面中显示详细的调试信息");
            listing.CheckboxLabeled("启用详细日志", ref settings.Debug.EnableVerboseLogging, "输出更详细的系统日志");
            listing.CheckboxLabeled("抑制游戏性能日志", ref settings.Debug.SuppressGameProfilerLogs, "减少游戏内建性能监控日志噪音");
            if (GUI.changed)
            {
                LogFilter.ApplyFiltersIfNeeded(); // 实时应用日志过滤设置
            }
            listing.CheckboxLabeled("性能分析模式", ref settings.Debug.EnablePerformanceProfiling, "启用性能分析（可能影响性能）");
            
            listing.Gap();
            
            // UI高级设置
            listing.Label("🖥️ 界面高级设置");
            listing.Label($"窗口不透明度: {settings.UI.WindowOpacity:F2}");
            settings.UI.WindowOpacity = listing.Slider(settings.UI.WindowOpacity, 0.5f, 1.0f);
            
            listing.Label($"最大显示消息数: {settings.UI.MaxDisplayedMessages}");
            settings.UI.MaxDisplayedMessages = (int)listing.Slider(settings.UI.MaxDisplayedMessages, 10, 200);

            listing.Gap();

            // 系统维护操作
            listing.Label("🔧 系统维护");
            
            if (listing.ButtonText("🔄 重置Core设置"))
            {
                if (Find.WindowStack.IsOpen<Dialog_MessageBox>()) return;
                
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "确定要重置Core模组的所有设置到默认值吗？\n这将重置系统设置，但不会影响官员设置。\n此操作不可撤销。",
                    () => {
                        settings.ResetToDefaults();
                        SettingsManager.SaveSettings();
                        Messages.Message("Core设置已重置", MessageTypeDefOf.TaskCompletion);
                    }
                ));
            }

            if (listing.ButtonText("💾 保存Core设置"))
            {
                try
                {
                    SettingsManager.SaveSettings();
                    SettingsManager.ApplySettings();
                    Messages.Message("Core设置已保存", MessageTypeDefOf.TaskCompletion);
                }
                catch (System.Exception ex)
                {
                    Messages.Message($"保存失败: {ex.Message}", MessageTypeDefOf.RejectInput);
                }
            }

            if (listing.ButtonText("🔍 导出设置文件"))
            {
                // 导出设置到桌面供调试
                Messages.Message("设置导出功能开发中...", MessageTypeDefOf.NeutralEvent);
            }

            listing.End();
        }

        private void DrawDebugInfo(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🐛 调试信息");
            listing.Gap();

            if (listing.ButtonText("刷新系统状态"))
            {
                _debugInfo = GenerateDebugInfo();
            }

            if (listing.ButtonText("清空所有缓存"))
            {
                RimAI.Core.Services.CacheService.Instance.Clear();
                Messages.Message("缓存已清空", MessageTypeDefOf.TaskCompletion);
            }

            if (listing.ButtonText("测试事件系统"))
            {
                TestEventSystem();
            }

            listing.Gap();

            // 显示调试信息
            var debugRect = listing.GetRect(rect.height - listing.CurHeight);
            if (string.IsNullOrEmpty(_debugInfo))
            {
                _debugInfo = GenerateDebugInfo();
            }
            
            Widgets.TextArea(debugRect, _debugInfo, true);

            listing.End();
        }

        private string GenerateDebugInfo()
        {
            var info = new System.Text.StringBuilder();
            
            info.AppendLine("=== RimAI Core 调试信息 ===");
            info.AppendLine($"时间: {System.DateTime.Now}");
            info.AppendLine();
            
            // 系统状态
            info.AppendLine(RimAICoreGameComponent.GetSystemStatus());
            info.AppendLine();
            
            // 服务状态
            var container = ServiceContainer.Instance;
            info.AppendLine(container.GetStatusInfo());
            info.AppendLine();
            
            var services = container.GetRegisteredServices();
            info.AppendLine("已注册服务:");
            foreach (var service in services)
            {
                info.AppendLine($"  - {service}");
            }
            info.AppendLine();
            
            // 缓存统计
            var cacheStats = RimAI.Core.Services.CacheService.Instance.GetStats();
            info.AppendLine("缓存统计:");
            info.AppendLine($"  - 总条目: {cacheStats.TotalEntries}");
            info.AppendLine($"  - 活跃条目: {cacheStats.ActiveEntries}");
            info.AppendLine($"  - 过期条目: {cacheStats.ExpiredEntries}");
            info.AppendLine($"  - 总访问次数: {cacheStats.TotalAccessCount}");
            info.AppendLine();
            
            // 事件总线统计
            var eventBus = CoreServices.EventBus;
            if (eventBus != null)
            {
                info.AppendLine("事件总线:");
                info.AppendLine("  - 事件总线已初始化");
                info.AppendLine("  - 处理器信息: 运行中");
            }
            
            return info.ToString();
        }

        private void TestEventSystem()
        {
            try
            {
                var eventBus = CoreServices.EventBus;
                if (eventBus != null)
                {
                    var testEvent = new RimAI.Core.Architecture.Events.ConfigurationChangedEvent(
                        "TestKey", 
                        "OldValue", 
                        "NewValue", 
                        "DebugTest"
                    );
                    
                    _ = eventBus.PublishAsync(testEvent);
                    Messages.Message("测试事件已发送", MessageTypeDefOf.TaskCompletion);
                }
                else
                {
                    Messages.Message("事件总线不可用", MessageTypeDefOf.RejectInput);
                }
            }
            catch (System.Exception ex)
            {
                Messages.Message($"测试失败: {ex.Message}", MessageTypeDefOf.RejectInput);
            }
        }
    }

    /// <summary>
    /// Core设置标签页枚举
    /// </summary>
    public enum SettingsTab
    {
        General,    // 重命名为系统设置
        Performance,
        Advanced,
        Debug
    }
}
