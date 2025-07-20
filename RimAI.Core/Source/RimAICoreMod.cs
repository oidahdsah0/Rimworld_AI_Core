using RimAI.Core.Settings;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core Mod主类 - 简化版本，参考Framework设计
    /// </summary>
    public class RimAICoreMod : Mod
    {
        private CoreSettings _settings;

        public RimAICoreMod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<CoreSettings>();
            
            // 🎯 修复崩溃：延迟设置管理器初始化，避免循环引用
            SettingsManager.SetSettings(_settings);
            
            Log.Message("[RimAICoreMod] RimAI Core mod loaded");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 🎯 修复崩溃：使用Framework风格的简单设置界面，避免复杂服务调用
            try
            {
                DrawSimpleSettings(inRect);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimAICoreMod] Settings window error: {ex.Message}");
                // 显示错误信息而不是崩溃
                Listing_Standard listing = new Listing_Standard();
                listing.Begin(inRect);
                listing.Label("❌ 设置界面加载失败");
                listing.Label($"错误: {ex.Message}");
                listing.Label("请查看日志获取详细信息");
                listing.End();
            }
        }

        private void DrawSimpleSettings(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // --- Core System Settings ---
            listingStandard.Label("🔧 RimAI Core 系统设置");
            listingStandard.GapLine();

            // 基础开关设置
            listingStandard.CheckboxLabeled("启用事件监控系统", ref _settings.Events.EnableEventBus, "启用自动事件检测和响应");
            
            if (_settings.Events.EnableEventBus)
            {
                listingStandard.CheckboxLabeled("  自动威胁检测", ref _settings.Events.EnableAutoThreatDetection, "自动检测威胁并提供建议");
                listingStandard.CheckboxLabeled("  资源监控", ref _settings.Events.EnableAutoResourceMonitoring, "监控资源短缺");
            }

            listingStandard.Gap();

            // UI设置
            listingStandard.Label("🖥️ 界面设置");
            listingStandard.CheckboxLabeled("显示性能统计", ref _settings.UI.ShowPerformanceStats, "在界面中显示性能信息");
            listingStandard.CheckboxLabeled("启用通知", ref _settings.UI.EnableNotifications, "显示系统通知消息");
            
            listingStandard.Gap();

            // 性能设置 (简化版)
            listingStandard.Label("⚡ 性能设置");
            listingStandard.Label($"最大并发请求数: {_settings.Performance.MaxConcurrentRequests}");
            _settings.Performance.MaxConcurrentRequests = (int)listingStandard.Slider(_settings.Performance.MaxConcurrentRequests, 1, 10);

            listingStandard.Gap();

            // 缓存设置
            listingStandard.Label("💾 缓存设置");
            listingStandard.CheckboxLabeled("启用缓存", ref _settings.Cache.EnableCaching, "启用智能缓存以提高性能");
            
            if (_settings.Cache.EnableCaching)
            {
                listingStandard.Label($"默认缓存时间(分钟): {_settings.Cache.DefaultCacheDurationMinutes}");
                _settings.Cache.DefaultCacheDurationMinutes = (int)listingStandard.Slider(_settings.Cache.DefaultCacheDurationMinutes, 1, 30);
            }

            listingStandard.Gap();

            // 信息显示（不调用复杂服务）
            listingStandard.Label("📊 状态信息");
            listingStandard.Label("ℹ️ 详细状态信息请使用主界面的系统诊断功能");

            listingStandard.Gap();

            // 操作按钮
            if (listingStandard.ButtonText("🔄 重置为默认设置"))
            {
                if (Find.WindowStack.IsOpen<Dialog_MessageBox>()) return;
                
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "确定要重置所有Core设置到默认值吗？\n此操作不可撤销。",
                    () => {
                        _settings.ResetToDefaults();
                        WriteSettings();
                        Messages.Message("设置已重置为默认值", MessageTypeDefOf.TaskCompletion);
                    }
                ));
            }

            if (GUI.changed)
            {
                // 自动保存设置变更
                WriteSettings();
            }

            listingStandard.End();
        }

        public override string SettingsCategory()
        {
            return "RimAI Core";
        }
    }
}
