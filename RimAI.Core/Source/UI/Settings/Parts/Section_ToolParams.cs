using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Modules.Embedding;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 工具匹配 - 参数（TopK 等）
    /// </summary>
    internal sealed class Section_ToolParams : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 工具匹配 - 参数");
            SettingsUIUtil.LabelWithTip(list, "工具匹配参数:", "用于控制工具检索与匹配的关键参数");

            int curTopK = draft?.Embedding?.TopK ?? 5;
            SettingsUIUtil.LabelWithTip(list, $"TopK: {curTopK}", "NarrowTopK 模式下给 LLM 的候选工具个数。");
            float topKf = list.Slider(curTopK, 1f, 20f);
            int newTopK = Mathf.Clamp(Mathf.RoundToInt(topKf), 1, 20);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            if (newTopK != curTopK)
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = draft?.Embedding?.Enabled ?? true,
                        TopK = newTopK,
                        MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig()
                    },
                    History = draft.History
                };
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = draft?.Embedding?.TopK ?? (cur.Embedding?.TopK ?? 5),
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = cur.Embedding?.Tools ?? new EmbeddingToolsConfig()
                            },
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘工具匹配-参数’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    draft = new CoreConfig
                    {
                        LLM = draft.LLM,
                        EventAggregator = draft.EventAggregator,
                        Orchestration = draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = true,
                            TopK = 5,
                            MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig()
                        },
                        History = draft.History
                    };
                });
            return draft;
        }
    }
}


