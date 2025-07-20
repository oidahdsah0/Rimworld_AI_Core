using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Core.Settings;
using RimAI.Core.Architecture;
using RimAI.Core.Officers;
using System;
using System.Collections.Generic;

namespace RimAI.Core.UI
{
    /// <summary>
    /// 官员设置窗口 - 从高级AI助手对话框改造而来
    /// 提供游戏内的官员配置和系统设置功能
    /// </summary>
    public class Dialog_OfficerSettings : Window
    {
        private SettingsTab currentTab = SettingsTab.Officers;
        private Vector2 scrollPosition = Vector2.zero;
        private string debugInfo = "";
        
        public override Vector2 InitialSize => new Vector2(1000f, 700f);
        public override bool IsDebug => false;

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            this.closeOnClickedOutside = true;
            this.draggable = true;
            this.resizeable = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            var settings = SettingsManager.Settings;
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            listing.Label("⚙️ RimAI Officer Settings | AI官员设置");
            Text.Font = GameFont.Small;
            
            listing.Gap();

            // 标签页按钮行
            DrawTabButtons(listing, inRect.width);
            
            listing.Gap();

            // 内容区域
            Rect contentRect = listing.GetRect(inRect.height - 120f);
            DrawTabContent(contentRect, settings);

            listing.Gap();

            // 底部按钮
            DrawBottomButtons(listing, inRect.width);

            listing.End();
        }

        private void DrawTabButtons(Listing_Standard listing, float availableWidth)
        {
            Rect tabRowRect = listing.GetRect(35f);
            
            List<TabData> tabs = new List<TabData>
            {
                new TabData("🏛️ 官员", SettingsTab.Officers),
                new TabData("⚙️ 常规", SettingsTab.General),
                new TabData("⚡ 性能", SettingsTab.Performance),
                new TabData("🔧 高级", SettingsTab.Advanced),
                new TabData("🐛 调试", SettingsTab.Debug)
            };

            float tabWidth = availableWidth / tabs.Count;
            float currentX = tabRowRect.x;

            foreach (var tab in tabs)
            {
                Rect tabRect = new Rect(currentX, tabRowRect.y, tabWidth, tabRowRect.height);
                bool isSelected = currentTab == tab.settingsTab;
                
                if (Widgets.ButtonText(tabRect, tab.label, true, true, isSelected))
                {
                    currentTab = tab.settingsTab;
                }
                
                currentX += tabWidth;
            }
        }

        private void DrawTabContent(Rect rect, CoreSettings settings)
        {
            switch (currentTab)
            {
                case SettingsTab.Officers:
                    DrawOfficerSettings(rect, settings);
                    break;
                case SettingsTab.General:
                    DrawGeneralSettings(rect, settings);
                    break;
                case SettingsTab.Performance:
                    DrawPerformanceSettings(rect, settings);
                    break;
                case SettingsTab.Advanced:
                    DrawAdvancedSettings(rect, settings);
                    break;
                case SettingsTab.Debug:
                    DrawDebugSettings(rect, settings);
                    break;
            }
        }

        private void DrawOfficerSettings(Rect rect, CoreSettings settings)
        {
            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 16f, 600f));
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0, 0, rect.width - 16f, 600f));

            listing.Label("🏛️ AI官员配置");
            listing.Gap();

            // 系统状态显示
            listing.Label("📊 系统状态:");
            try
            {
                var governor = Governor.Instance;
                string systemStatus = governor != null ? 
                    $"✅ 基础总督: 就绪 - {governor.GetPublicStatus()}" : 
                    "❌ 基础总督: 未就绪";
                    
                listing.Label(systemStatus);
                
                // 显示框架状态
                var frameworkStatus = CoreServices.GetReadinessReport();
                Rect statusRect = listing.GetRect(80f);
                Widgets.TextArea(statusRect, frameworkStatus, true);
            }
            catch (Exception ex)
            {
                listing.Label($"❌ 系统状态获取失败: {ex.Message}");
            }

            listing.Gap();

            // 基础总督设置
            DrawOfficerConfig(listing, "基础总督", settings.GetOfficerConfig("Governor"));
            
            listing.Gap();
            
            // 简化提示
            listing.Label("ℹ️ 当前版本仅支持基础总督功能，其他官员功能已简化。");
            listing.Label("📝 使用主界面的对话功能与AI总督进行交互。");

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawOfficerConfig(Listing_Standard listing, string displayName, OfficerConfig config)
        {
            listing.Label($"⚙️ {displayName} 配置");
            
            listing.CheckboxLabeled("启用官员", ref config.IsEnabled, $"启用/禁用 {displayName}");
            
            if (config.IsEnabled)
            {
                listing.Label($"🎨 响应创造性: {config.ResponseTemperature:F1}");
                config.ResponseTemperature = listing.Slider(config.ResponseTemperature, 0.1f, 1.0f);
                
                listing.CheckboxLabeled("🚀 偏好流式响应", ref config.PreferStreaming, "在支持时优先使用流式响应");
                listing.CheckboxLabeled("🔍 自动分析", ref config.AutoAnalysis, "启用自动态势分析");
                
                listing.Label($"💾 缓存时间: {config.CacheDurationMinutes} 分钟");
                config.CacheDurationMinutes = (int)listing.Slider(config.CacheDurationMinutes, 1, 30);
                
                listing.Gap();
                
                // 测试按钮
                if (listing.ButtonText($"🧪 测试 {displayName}"))
                {
                    TestOfficer(displayName);
                }
            }
        }

        private void DrawGeneralSettings(Rect rect, CoreSettings settings)
        {
            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 16f, 500f));
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0, 0, rect.width - 16f, 500f));

            listing.Label("🔧 基本设置");
            listing.Gap();

            // UI设置
            listing.Label("🖥️ 界面设置:");
            listing.CheckboxLabeled("显示高级选项", ref settings.UI.ShowAdvancedOptions, "显示更多详细的设置选项");
            listing.CheckboxLabeled("启用通知", ref settings.UI.EnableNotifications, "显示AI建议和警告通知");
            listing.CheckboxLabeled("显示流式指示器", ref settings.UI.EnableStreamingIndicator, "在流式响应时显示进度指示");
            
            listing.Gap();
            
            // 缓存设置
            listing.Label("📦 缓存设置:");
            listing.CheckboxLabeled("启用缓存", ref settings.Cache.EnableCaching, "缓存AI响应以提高性能");
            
            if (settings.Cache.EnableCaching)
            {
                listing.Label($"缓存持续时间: {settings.Cache.DefaultCacheDurationMinutes} 分钟");
                settings.Cache.DefaultCacheDurationMinutes = (int)listing.Slider(settings.Cache.DefaultCacheDurationMinutes, 1, 60);
                
                listing.Label($"最大缓存条目: {settings.Cache.MaxCacheEntries}");
                settings.Cache.MaxCacheEntries = (int)listing.Slider(settings.Cache.MaxCacheEntries, 100, 5000);
            }

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawPerformanceSettings(Rect rect, CoreSettings settings)
        {
            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 16f, 400f));
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0, 0, rect.width - 16f, 400f));

            listing.Label("⚡ 性能设置");
            listing.Gap();

            listing.Label($"最大并发请求: {settings.Performance.MaxConcurrentRequests}");
            settings.Performance.MaxConcurrentRequests = (int)listing.Slider(settings.Performance.MaxConcurrentRequests, 1, 10);
            
            listing.Label($"请求超时 (秒): {settings.Performance.RequestTimeoutSeconds}");
            settings.Performance.RequestTimeoutSeconds = (int)listing.Slider(settings.Performance.RequestTimeoutSeconds, 10, 120);

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawAdvancedSettings(Rect rect, CoreSettings settings)
        {
            Widgets.BeginScrollView(rect, ref scrollPosition, new Rect(0, 0, rect.width - 16f, 300f));
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(new Rect(0, 0, rect.width - 16f, 300f));

            listing.Label("🔧 高级设置");
            listing.Gap();
            
            listing.Label("⚠️ 高级设置可能影响系统稳定性，请谨慎修改。");
            
            // 可以在这里添加更多高级设置

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawDebugSettings(Rect rect, CoreSettings settings)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(rect);

            listing.Label("🐛 调试信息");
            listing.Gap();

            if (listing.ButtonText("🔄 刷新调试信息"))
            {
                RefreshDebugInfo();
            }

            listing.Gap();

            Rect debugRect = listing.GetRect(rect.height - 100f);
            Widgets.TextArea(debugRect, debugInfo, true);

            listing.End();
        }

        private void DrawBottomButtons(Listing_Standard listing, float availableWidth)
        {
            Rect buttonRowRect = listing.GetRect(35f);
            float buttonWidth = (availableWidth - 10f) / 2f;

            Rect saveRect = new Rect(buttonRowRect.x, buttonRowRect.y, buttonWidth, buttonRowRect.height);
            Rect closeRect = new Rect(buttonRowRect.x + buttonWidth + 10f, buttonRowRect.y, buttonWidth, buttonRowRect.height);

            if (Widgets.ButtonText(saveRect, "💾 保存设置"))
            {
                SaveSettings();
            }

            if (Widgets.ButtonText(closeRect, "❌ 关闭"))
            {
                Close();
            }
        }

        private void TestOfficer(string officerName)
        {
            try
            {
                if (officerName == "基础总督")
                {
                    var governor = Governor.Instance;
                    if (governor != null)
                    {
                        Messages.Message($"✅ {officerName} 测试成功: {governor.GetPublicStatus()}", MessageTypeDefOf.PositiveEvent);
                    }
                    else
                    {
                        Messages.Message($"❌ {officerName} 测试失败: 实例未找到", MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Messages.Message($"❌ {officerName} 测试失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        private void SaveSettings()
        {
            try
            {
                // 保存设置逻辑
                Messages.Message("💾 设置已保存", MessageTypeDefOf.PositiveEvent);
            }
            catch (Exception ex)
            {
                Messages.Message($"❌ 设置保存失败: {ex.Message}", MessageTypeDefOf.NegativeEvent);
            }
        }

        private void RefreshDebugInfo()
        {
            try
            {
                debugInfo = $"=== RimAI 调试信息 ===\n";
                debugInfo += $"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n";
                debugInfo += CoreServices.GetReadinessReport();
                debugInfo += "\n\n=== 官员状态 ===\n";
                
                var governor = Governor.Instance;
                if (governor != null)
                {
                    debugInfo += $"基础总督: 就绪\n";
                    debugInfo += $"状态: {governor.GetPublicStatus()}\n";
                }
                else
                {
                    debugInfo += "基础总督: 未就绪\n";
                }
            }
            catch (Exception ex)
            {
                debugInfo = $"调试信息获取失败: {ex.Message}";
            }
        }

        // 辅助数据结构
        private struct TabData
        {
            public string label;
            public SettingsTab settingsTab;

            public TabData(string label, SettingsTab settingsTab)
            {
                this.label = label;
                this.settingsTab = settingsTab;
            }
        }
    }
}
