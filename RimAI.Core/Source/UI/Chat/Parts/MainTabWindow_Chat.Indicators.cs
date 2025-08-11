using System;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 指示灯行渲染与触发。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        private void DrawIndicatorsBar(Rect rect)
        {
            // 背景轻微分隔
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.05f));

            float pad = 6f;
            float lightW = 28f; // 小长方形，复古风
            float lightH = 10f;
            float gap = 10f;

            // 计算标签尺寸（灰色小标签）
            var oldFont = Text.Font; var oldAnchor = Text.Anchor; var oldColor = GUI.color;
            Text.Font = GameFont.Tiny;
            var dataSize = Text.CalcSize("Data");
            var finishSize = Text.CalcSize("Finish");

            var redRect = new Rect(rect.x + pad, rect.y + (rect.height - lightH) / 2f, lightW, lightH);
            var dataLabelRect = new Rect(redRect.xMax + 6f, rect.y, dataSize.x, rect.height);
            var greenRect = new Rect(dataLabelRect.xMax + gap, redRect.y, lightW, lightH);
            var finishLabelRect = new Rect(greenRect.xMax + 6f, rect.y, finishSize.x, rect.height);

            DrawIndicatorLight(redRect, isOn: DateTime.UtcNow <= _indicatorRedUntilUtc, baseColor: new Color(0.9f, 0.2f, 0.2f), onBrightness: 1f, offBrightness: 0.25f);
            DrawIndicatorLight(greenRect, isOn: DateTime.UtcNow <= _indicatorGreenUntilUtc, baseColor: new Color(0.2f, 0.85f, 0.35f), onBrightness: 1f, offBrightness: 0.25f);

            // 绘制灰色标签
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
            Widgets.Label(dataLabelRect, "Data");
            Widgets.Label(finishLabelRect, "Finish");

            // 还原
            GUI.color = oldColor; Text.Font = oldFont; Text.Anchor = oldAnchor;
        }

        private static void DrawIndicatorLight(Rect rect, bool isOn, Color baseColor, float onBrightness, float offBrightness)
        {
            // 阴影
            var shadow = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
            Widgets.DrawBoxSolid(shadow, new Color(0f, 0f, 0f, 0.25f));

            // 边框
            Widgets.DrawBoxSolidWithOutline(rect, new Color(0f, 0f, 0f, 0.18f), new Color(0f, 0f, 0f, 0.45f));

            // 灯体（亮度控制）
            float b = isOn ? onBrightness : offBrightness;
            var col = new Color(Mathf.Clamp01(baseColor.r * b), Mathf.Clamp01(baseColor.g * b), Mathf.Clamp01(baseColor.b * b), 1f);
            var inner = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f);
            Widgets.DrawBoxSolid(inner, col);
        }

        private void TriggerIndicatorOnChunk()
        {
            // 闪红灯 300ms
            _indicatorRedUntilUtc = DateTime.UtcNow.AddMilliseconds(300);
            TryPlayIndicatorSound();
        }

        private void TriggerIndicatorOnCompleted()
        {
            // 亮绿灯 1200ms
            _indicatorGreenUntilUtc = DateTime.UtcNow.AddMilliseconds(1200);
            TryPlayIndicatorSound();
        }

        private void TryPlayIndicatorSound()
        {
            // 预留：读取磁盘音效（用户提供 AudioClip 或 Verse.SoundDef 后接入）
            // 节流，避免过于频繁
            var now = DateTime.UtcNow;
            if ((now - _lastIndicatorSoundAtUtc).TotalMilliseconds < MinIndicatorSoundIntervalMs) return;
            _lastIndicatorSoundAtUtc = now;
            // TODO: 播放音效（占位）
            // Example: RimWorld.SoundDefOf.PageChange.PlayOneShotOnCamera();
        }
    }
}


