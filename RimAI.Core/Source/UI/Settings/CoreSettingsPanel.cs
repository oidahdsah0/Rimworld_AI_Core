using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Modules.Embedding;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using System.Runtime.InteropServices;
using RimAI.Core.UI.Settings.Parts;

namespace RimAI.Core.UI.Settings
{
    /// <summary>
    /// RimAI Core 设置面板（仅负责绘制与应用配置）。
    /// </summary>
    internal sealed class CoreSettingsPanel
    {
        private CoreConfig _draft;

        public CoreSettingsPanel()
        {
            try
            {
                var cfg = CoreServices.Locator.Get<IConfigurationService>();
                _draft = cfg?.Current ?? CoreConfig.CreateDefault();
            }
            catch
            {
                _draft = CoreConfig.CreateDefault();
            }
        }

        private Vector2 _scrollPos;
        private float _scrollHeight;

        public void Draw(Rect inRect)
        {
            if (_draft == null) _draft = CoreConfig.CreateDefault();
            var list = new Listing_Standard();

            // 预留滚动条区域（固定内容高度，避免自动计算导致控件不渲染）
            var outRect = inRect;
            var contentHeight = 2600f; // 固定初始高度，可按需调整
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(outRect, ref _scrollPos, viewRect);
            list.Begin(viewRect);
            list.ColumnWidth = viewRect.width;

            int section = 1;
            // 用分区实现替换原始内联绘制
            ISettingsSection[] sections = new ISettingsSection[]
            {
                new Section_ToolMode(),
                new Section_ToolParams(),
                new Section_ToolThresholds(),
                new Section_IndexBuild(),
                new Section_DynamicThresholds(),
                new Section_Planner(),
                new Section_ProgressTemplates(),
                new Section_History(),
                new Section_IndexManager()
            };
            foreach (var sec in sections)
            {
                list.GapLine();
                _draft = sec.Draw(list, ref section, _draft);
            }

            list.End();
            _scrollHeight = list.CurHeight; // 当前不参与布局，仅保留为开发参考
            Widgets.EndScrollView();
        }

        // 所有通用绘制工具/逻辑已移动到 Parts/SettingsUIUtil 与各 Section。
    }
}


