using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 设置绘制常用工具。
    /// </summary>
    internal static class SettingsUIUtil
    {
        public static float UIControlSpacing = 5f;

        public static void SectionTitle(Listing_Standard list, string title)
        {
            var old = Text.Font;
            Text.Font = GameFont.Medium;
            var rect = list.GetRect(Text.LineHeight + 6f);
            Widgets.Label(rect, title);
            Text.Font = old;
        }

        public static void LabelWithTip(Listing_Standard list, string label, string tip)
        {
            var rect = list.GetRect(Text.LineHeight);
            Widgets.Label(rect, label);
            if (!string.IsNullOrEmpty(tip))
            {
                TooltipHandler.TipRegion(rect, tip);
            }
        }

        public static bool DrawResetButton(Listing_Standard list, string label)
        {
            var row = list.GetRect(28f);
            float width = 160f;
            var rect = new Rect(row.xMax - width, row.y, width, row.height);
            return Widgets.ButtonText(rect, label);
        }

        public static void DrawSaveResetRow(Listing_Standard list, string saveLabel, System.Action onSave, string resetLabel, System.Action onReset)
        {
            var row = list.GetRect(28f);
            float w = 160f;
            float gap = 8f;
            var saveRect = new Rect(row.xMax - (w * 2 + gap), row.y, w, row.height);
            var resetRect = new Rect(row.xMax - w, row.y, w, row.height);
            if (Widgets.ButtonText(saveRect, saveLabel)) onSave?.Invoke();
            if (Widgets.ButtonText(resetRect, resetLabel)) onReset?.Invoke();
        }
    }
}


