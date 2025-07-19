using RimAI.Core.Architecture;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.Settings
{
    /// <summary>
    /// Core设置窗口
    /// </summary>
    public class CoreSettingsWindow
    {
        private Vector2 _scrollPosition;
        private SettingsTab _currentTab = SettingsTab.General;
        private float _tabHeight = 30f;
        private string _debugInfo = "";

        public void DoWindowContents(Rect inRect)
        {
            var settings = SettingsManager.Settings;
            
            // 标签页
            var tabRect = new Rect(0, 0, inRect.width, _tabHeight);
            DrawTabs(tabRect);

            // 内容区域
            var contentRect = new Rect(0, _tabHeight + 10, inRect.width, inRect.height - _tabHeight - 10);
            
            switch (_currentTab)
            {
                case SettingsTab.General:
                    DrawGeneralSettings(contentRect, settings);
                    break;
                case SettingsTab.Officers:
                    DrawOfficerSettings(contentRect, settings);
                    break;
                case SettingsTab.Performance:
                    DrawPerformanceSettings(contentRect, settings);
                    break;
                case SettingsTab.Advanced:
                    DrawAdvancedSettings(contentRect, settings);
                    break;
                case SettingsTab.Debug:
                    DrawDebugInfo(contentRect);
                    break;
            }
        }

        private void DrawTabs(Rect rect)
        {
            var tabWidth = rect.width / 5;
            var tabRects = new[]
            {
                new Rect(0, 0, tabWidth, rect.height),
                new Rect(tabWidth, 0, tabWidth, rect.height),
                new Rect(tabWidth * 2, 0, tabWidth, rect.height),
                new Rect(tabWidth * 3, 0, tabWidth, rect.height),
                new Rect(tabWidth * 4, 0, tabWidth, rect.height)
            };

            var tabNames = new[] { "常规", "官员", "性能", "高级", "调试" };
            var tabs = new[] 
            { 
                SettingsTab.General, 
                SettingsTab.Officers, 
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

        private void DrawGeneralSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🔧 基本设置");
            listing.Gap();

            // UI设置
            listing.CheckboxLabeled("显示高级选项", ref settings.UI.ShowAdvancedOptions, "显示更多详细的设置选项");
            listing.CheckboxLabeled("启用通知", ref settings.UI.EnableNotifications, "显示AI建议和警告通知");
            listing.CheckboxLabeled("显示流式指示器", ref settings.UI.EnableStreamingIndicator, "在流式响应时显示进度指示");
            
            listing.Gap();
            
            // 缓存设置
            listing.Label("📦 缓存设置");
            listing.CheckboxLabeled("启用缓存", ref settings.Cache.EnableCaching, "缓存AI响应以提高性能");
            
            if (settings.Cache.EnableCaching)
            {
                listing.Label($"缓存持续时间: {settings.Cache.DefaultCacheDurationMinutes} 分钟");
                settings.Cache.DefaultCacheDurationMinutes = (int)listing.Slider(settings.Cache.DefaultCacheDurationMinutes, 1, 60);
                
                listing.Label($"最大缓存条目: {settings.Cache.MaxCacheEntries}");
                settings.Cache.MaxCacheEntries = (int)listing.Slider(settings.Cache.MaxCacheEntries, 100, 5000);
            }

            listing.Gap();
            
            // 事件设置
            listing.Label("📡 事件监控");
            listing.CheckboxLabeled("启用事件总线", ref settings.Events.EnableEventBus, "启用事件系统以支持自动响应");
            
            if (settings.Events.EnableEventBus)
            {
                listing.CheckboxLabeled("自动威胁检测", ref settings.Events.EnableAutoThreatDetection, "自动检测和响应威胁");
                listing.CheckboxLabeled("自动资源监控", ref settings.Events.EnableAutoResourceMonitoring, "监控资源短缺");
                listing.CheckboxLabeled("殖民者状态监控", ref settings.Events.EnableAutoColonistMonitoring, "监控殖民者健康和心情");
            }

            listing.Gap();

            // 系统状态
            listing.Label("📊 系统状态");
            var statusInfo = CoreServices.GetReadinessReport();
            var statusRect = listing.GetRect(100);
            Widgets.TextArea(statusRect, statusInfo, true);

            listing.End();
        }

        private void DrawOfficerSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🤖 AI官员设置");
            listing.Gap();

            // 总督设置
            DrawOfficerConfig(listing, "智能总督", settings.GetOfficerConfig("SmartGovernor"));
            listing.Gap();

            // 军事官员设置  
            DrawOfficerConfig(listing, "军事指挥官", settings.GetOfficerConfig("MilitaryOfficer"));
            listing.Gap();

            // 后勤官员设置
            DrawOfficerConfig(listing, "后勤总监", settings.GetOfficerConfig("LogisticsOfficer"));

            listing.End();
        }

        private void DrawOfficerConfig(Listing_Standard listing, string displayName, OfficerConfig config)
        {
            listing.Label($"⚙️ {displayName}");
            
            listing.CheckboxLabeled("启用", ref config.IsEnabled, $"启用/禁用 {displayName}");
            
            if (config.IsEnabled)
            {
                listing.Label($"响应创造性 (Temperature): {config.ResponseTemperature:F1}");
                config.ResponseTemperature = listing.Slider(config.ResponseTemperature, 0.1f, 1.0f);
                
                listing.CheckboxLabeled("偏好流式响应", ref config.PreferStreaming, "在支持时优先使用流式响应");
                listing.CheckboxLabeled("自动分析", ref config.AutoAnalysis, "启用自动态势分析");
                
                listing.Label($"缓存时间: {config.CacheDurationMinutes} 分钟");
                config.CacheDurationMinutes = (int)listing.Slider(config.CacheDurationMinutes, 1, 30);
            }
        }

        private void DrawPerformanceSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("⚡ 性能设置");
            listing.Gap();

            // 并发设置
            listing.Label("🔄 并发控制");
            listing.Label($"最大并发请求: {settings.Performance.MaxConcurrentRequests}");
            settings.Performance.MaxConcurrentRequests = (int)listing.Slider(settings.Performance.MaxConcurrentRequests, 1, 10);
            
            listing.Label($"请求超时 (秒): {settings.Performance.RequestTimeoutSeconds}");
            settings.Performance.RequestTimeoutSeconds = (int)listing.Slider(settings.Performance.RequestTimeoutSeconds, 10, 120);
            
            listing.Gap();

            // 分析设置
            listing.Label("📈 分析设置");
            listing.CheckboxLabeled("启用后台分析", ref settings.Performance.EnableBackgroundAnalysis, "在后台持续分析殖民地状态");
            
            if (settings.Performance.EnableBackgroundAnalysis)
            {
                listing.Label($"分析间隔 (游戏小时): {settings.Performance.AnalysisIntervalTicks / 2500f:F1}");
                settings.Performance.AnalysisIntervalTicks = (int)listing.Slider(settings.Performance.AnalysisIntervalTicks, 1250, 25000);
                
                listing.Label($"最大后台任务: {settings.Performance.MaxBackgroundTasks}");
                settings.Performance.MaxBackgroundTasks = (int)listing.Slider(settings.Performance.MaxBackgroundTasks, 1, 5);
            }

            listing.Gap();

            // 批处理设置
            listing.CheckboxLabeled("启用批处理", ref settings.Performance.EnableBatchProcessing, "将多个请求合并处理以提高效率");

            listing.Gap();

            // 性能统计
            if (settings.UI.ShowPerformanceStats)
            {
                listing.Label("📊 性能统计");
                var cacheStats = RimAI.Core.Services.CacheService.Instance.GetStats();
                listing.Label($"缓存命中率: {(cacheStats.TotalAccessCount > 0 ? (cacheStats.ActiveEntries / (float)cacheStats.TotalAccessCount * 100) : 0):F1}%");
                listing.Label($"活跃缓存条目: {cacheStats.ActiveEntries}/{cacheStats.TotalEntries}");
            }

            listing.CheckboxLabeled("显示性能统计", ref settings.UI.ShowPerformanceStats);

            listing.End();
        }

        private void DrawAdvancedSettings(Rect rect, CoreSettings settings)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🔬 高级设置");
            listing.Gap();

            listing.CheckboxLabeled("显示调试信息", ref settings.UI.ShowDebugInfo, "在界面中显示详细的调试信息");
            
            listing.Gap();
            
            listing.Label($"窗口不透明度: {settings.UI.WindowOpacity:F2}");
            settings.UI.WindowOpacity = listing.Slider(settings.UI.WindowOpacity, 0.5f, 1.0f);
            
            listing.Label($"最大显示消息数: {settings.UI.MaxDisplayedMessages}");
            settings.UI.MaxDisplayedMessages = (int)listing.Slider(settings.UI.MaxDisplayedMessages, 10, 200);

            listing.Gap();

            // 重置设置按钮
            if (listing.ButtonText("重置所有设置"))
            {
                if (Find.WindowStack.IsOpen<Dialog_MessageBox>()) return;
                
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "确定要重置所有设置到默认值吗？此操作不可撤销。",
                    () => {
                        settings.ResetToDefaults();
                        SettingsManager.SaveSettings();
                        Messages.Message("设置已重置", MessageTypeDefOf.TaskCompletion);
                    }
                ));
            }

            if (listing.ButtonText("保存设置"))
            {
                SettingsManager.SaveSettings();
                SettingsManager.ApplySettings();
                Messages.Message("设置已保存", MessageTypeDefOf.TaskCompletion);
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
            
            Widgets.TextAreaScrollable(debugRect, _debugInfo, ref _scrollPosition, true);

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
                info.AppendLine($"  - 威胁检测处理器: {eventBus.GetHandlerCount<RimAI.Core.Architecture.Events.ThreatDetectedEvent>()}");
                info.AppendLine($"  - 配置变更处理器: {eventBus.GetHandlerCount<RimAI.Core.Architecture.Events.ConfigurationChangedEvent>()}");
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
    /// 设置标签页枚举
    /// </summary>
    public enum SettingsTab
    {
        General,
        Officers,
        Performance,
        Advanced,
        Debug
    }
}
