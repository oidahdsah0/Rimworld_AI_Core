using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure.Localization;

namespace RimAI.Core.Source.UI.ServerChatWindow.Parts
{
    internal static class ServerPersonaTab
    {
        public static void Draw(
            Rect inRect,
            ref string selectedPresetKey,
            ref string personaName,
            ref string personaContent,
            ref Vector2 scroll,
            System.Action<string, string, string> onSelectPreset,
            System.Action onSave,
            System.Action onClearOverride)
        {
            // 行高与间距
            const float rowPad = 6f;
            const float titleH = 36f;   // 行1：标题栏
            const float dropdownH = 32f; // 行2：下拉菜单
            const float actionsH = 34f; // 操作栏高度
            const float headerTitleH = 30f; // 顶部大标题高度
            const float titleInputMaxWidth = 380f; // 标题文本框的最大显示宽度（像素）
            const int titleMaxChars = 32; // 标题最大字符数（超出将截断）
            const float dropdownMaxWidth = 380f; // 下拉按钮的最大显示宽度（像素）
            const float contentReducePx = 20f; // 内容区高度减少值（像素）

            // 顶部：大标题 + 说明
            var headerTitleRect = new Rect(inRect.x + 8f, inRect.y, inRect.width - 16f, headerTitleH);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerTitleRect, "RimAI.SCW.Persona.HeaderTitle".Translate());
            Text.Font = GameFont.Small;
            string headerDesc = "RimAI.SCW.Persona.HeaderDesc".Translate().Resolve();
            float headerDescH = Mathf.Ceil(Text.CalcHeight(headerDesc, headerTitleRect.width));
            var headerDescRect = new Rect(inRect.x + 8f, headerTitleRect.yMax + 2f, headerTitleRect.width, headerDescH);
            var oldColorHeader = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            Widgets.Label(headerDescRect, headerDesc);
            GUI.color = oldColorHeader;

            float yStart = headerDescRect.yMax + rowPad;

            // 第1行：标题栏（单行文本框，可编辑 + 标签）
            var row1 = new Rect(inRect.x, yStart, inRect.width, titleH);
            var r1Label = new Rect(row1.x + 8f, row1.y + 4f, 90f, 24f);
            Widgets.Label(r1Label, "RimAI.SCW.Persona.TitleLabel".Translate());
            var availableW = row1.width - (r1Label.width + 8f + 6f + 8f);
            var inputW = Mathf.Min(titleInputMaxWidth, availableW);
            var r1Input = new Rect(r1Label.xMax + 6f, row1.y + 2f, inputW, 28f);
            Text.Font = GameFont.Small;
            personaName = Widgets.TextField(r1Input, personaName ?? string.Empty);
            if (!string.IsNullOrEmpty(personaName) && personaName.Length > titleMaxChars)
                personaName = personaName.Substring(0, Mathf.Min(titleMaxChars, personaName.Length));

            // 第2行：下拉菜单（独立一行）
            var row2 = new Rect(inRect.x, row1.yMax + rowPad, inRect.width, dropdownH);
            var labelRect = new Rect(row2.x + 8f, row2.y + 7f, 90f, 22f); // 向下偏移5像素，与下拉按钮文本对齐
            Widgets.Label(labelRect, "RimAI.SCW.Persona.TemplateLabel".Translate());
            var dropdownAvailableW = row2.width - (labelRect.width + 8f + 6f + 8f);
            var dropdownW = Mathf.Min(dropdownMaxWidth, dropdownAvailableW);
            var dropdownRect = new Rect(labelRect.xMax + 6f, row2.y + 2f, dropdownW, 28f);
            if (Widgets.ButtonText(dropdownRect, string.IsNullOrWhiteSpace(personaName) ? "RimAI.SCW.Persona.TemplateButtonDefault".Translate() : personaName))
            {
                var options = LoadServerPersonaOptions();
                if (options.Count > 0)
                {
                    var menu = new List<FloatMenuOption>();
                    foreach (var opt in options)
                    {
                        var key = opt.key; var title = opt.title; var text = opt.text;
                        menu.Add(new FloatMenuOption(title, () => { onSelectPreset?.Invoke(key, title, text); }));
                    }
                    Find.WindowStack.Add(new FloatMenu(menu));
                }
            }

            // 第3行：内容区（可编辑，滚动）
            var headerBlockH = headerTitleH + 2f + headerDescH + rowPad; // 顶部区块总高度
            var row3 = new Rect(inRect.x, row2.yMax + rowPad, inRect.width, inRect.height - (headerBlockH + titleH + dropdownH + rowPad) - actionsH - contentReducePx);
            var contentOuter = row3.ContractedBy(8f);
            var viewRect = new Rect(0f, 0f, contentOuter.width - 16f, Mathf.Max(contentOuter.height, Text.CalcHeight(personaContent ?? string.Empty, contentOuter.width - 16f) + 12f));
            Widgets.BeginScrollView(contentOuter, ref scroll, viewRect);
            personaContent = Widgets.TextArea(viewRect, personaContent ?? string.Empty);
            if (string.IsNullOrWhiteSpace(personaContent))
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                // 放在视图内左上角一点内边距
                var placeholderRect = new Rect(6f, 6f, viewRect.width - 12f, 22f);
                Widgets.Label(placeholderRect, "RimAI.SCW.Persona.Placeholder".Translate());
                GUI.color = prev;
            }
            Widgets.EndScrollView();

            // 操作区：保存 / 清空覆盖（位于最底部）
            var bar = new Rect(inRect.x, row3.yMax + 4f, inRect.width, actionsH - 4f);
            float bw = 110f; float sp = 8f;
            var rSave = new Rect(bar.xMax - bw, bar.y, bw, 28f);
            var rClear = new Rect(rSave.x - sp - bw, bar.y, bw, 28f);
            if (Widgets.ButtonText(rSave, "RimAI.SCW.Persona.Save".Translate())) onSave?.Invoke();
            if (Widgets.ButtonText(rClear, "RimAI.SCW.Persona.ClearOverride".Translate())) onClearOverride?.Invoke();
        }

        private static List<(string key, string title, string text)> LoadServerPersonaOptions()
        {
            var results = new List<(string, string, string)>();
            try
            {
                var container = RimAICoreMod.Container;
                var loc = container.Resolve<ILocalizationService>();
                var cfgInternal = container.Resolve<RimAI.Core.Contracts.Config.IConfigurationService>() as RimAI.Core.Source.Infrastructure.Configuration.ConfigurationService;
                var overrideLocale = cfgInternal?.GetPromptLocaleOverrideOrNull();
                var locale = string.IsNullOrWhiteSpace(overrideLocale)
                    ? (loc?.GetDefaultLocale() ?? cfgInternal?.GetInternal()?.General?.Locale ?? "en")
                    : overrideLocale;
                var json = loc?.Get(locale, "server.prompts.json", string.Empty);
                if (string.IsNullOrWhiteSpace(json) && !string.Equals(locale, "en", System.StringComparison.OrdinalIgnoreCase))
                {
                    // 回退到英文资源，避免缺失语言时下拉无数据
                    json = loc?.Get("en", "server.prompts.json", string.Empty);
                }
                if (string.IsNullOrWhiteSpace(json)) return results;
                var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
                // 兼容字段名 baseOptions / BaseOptions
                var opts = jo[(object)"baseOptions"] as Newtonsoft.Json.Linq.JArray
                           ?? jo[(object)"BaseOptions"] as Newtonsoft.Json.Linq.JArray;
                if (opts == null) return results;
                foreach (var it in opts)
                {
                    var key = it[(object)"key"]?.ToString();
                    var title = it[(object)"title"]?.ToString();
                    var text = it[(object)"text"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(title))
                        results.Add((key, title, text ?? string.Empty));
                }
            }
            catch { }
            return results;
        }
    }
}
