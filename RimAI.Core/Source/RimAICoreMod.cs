using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core Mod主类
    /// </summary>
    public class RimAICoreMod : Mod
    {
        private CoreSettings _settings;
        private CoreSettingsWindow _settingsWindow;

        public RimAICoreMod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<CoreSettings>();
            _settingsWindow = new CoreSettingsWindow();
            
            // 🎯 修复崩溃：提前设置到管理器，避免循环引用
            SettingsManager.SetSettings(_settings);
            
            Log.Message("[RimAICoreMod] RimAI Core mod loaded");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            try
            {
                // 🎯 修复崩溃：直接使用实例设置，避免循环引用
                _settingsWindow.DoWindowContents(inRect, _settings);
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

        public override string SettingsCategory()
        {
            return "RimAI Core";
        }
    }
}
