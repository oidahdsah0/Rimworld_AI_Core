using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Modules.Embedding;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 工具匹配 - 阈值与权重
    /// </summary>
    internal sealed class Section_ToolThresholds : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 工具匹配 - 阈值与权重");
            var toolsCfg = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();

            float curTop1 = (float)(toolsCfg?.Top1Threshold ?? 0.82);
            SettingsUIUtil.LabelWithTip(list, $"Top1 阈值: {curTop1:F2}", "FastTop1 模式的最低命中置信度。");
            float newTop1 = list.Slider(curTop1, 0.50f, 0.95f);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            float curLight = (float)(toolsCfg?.LightningTop1Threshold ?? 0.86);
            SettingsUIUtil.LabelWithTip(list, $"Lightning Top1 阈值: {curLight:F2}", "闪电直出模式阈值，应≥Top1。");
            float newLight = list.Slider(curLight, 0.60f, 0.99f);
            newLight = Mathf.Max(newLight, newTop1 + 0.01f);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            if (!Mathf.Approximately(newTop1, (float)curTop1) || !Mathf.Approximately(newLight, (float)curLight))
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = draft?.Embedding?.Enabled ?? true,
                        TopK = draft?.Embedding?.TopK ?? 5,
                        MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg?.Mode ?? "Classic",
                            Top1Threshold = newTop1,
                            LightningTop1Threshold = newLight,
                            IndexPath = toolsCfg?.IndexPath ?? "auto",
                            AutoBuildOnStart = toolsCfg?.AutoBuildOnStart ?? true,
                            BlockDuringBuild = toolsCfg?.BlockDuringBuild ?? true,
                            ScoreWeights = toolsCfg?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                            DynamicThresholds = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                        }
                    },
                    History = draft.History
                };
                toolsCfg = draft.Embedding.Tools;
            }

            double curWName = toolsCfg?.ScoreWeights?.Name ?? 0.6;
            SettingsUIUtil.LabelWithTip(list, $"分数权重 Name: {curWName:F2} / Description: {(1 - curWName):F2}", "控制 name/description 权重");
            float wNameF = list.Slider((float)curWName, 0f, 1f);
            float wDescF = 1f - wNameF;
            list.Gap(SettingsUIUtil.UIControlSpacing);
            if (!Mathf.Approximately(wNameF, (float)curWName))
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = draft?.Embedding?.Enabled ?? true,
                        TopK = draft?.Embedding?.TopK ?? 5,
                        MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg?.Mode ?? "Classic",
                            Top1Threshold = toolsCfg?.Top1Threshold ?? 0.82,
                            LightningTop1Threshold = toolsCfg?.LightningTop1Threshold ?? 0.86,
                            IndexPath = toolsCfg?.IndexPath ?? "auto",
                            AutoBuildOnStart = toolsCfg?.AutoBuildOnStart ?? true,
                            BlockDuringBuild = toolsCfg?.BlockDuringBuild ?? true,
                            ScoreWeights = new EmbeddingToolsScoreWeights { Name = wNameF, Description = wDescF },
                            DynamicThresholds = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                        }
                    },
                    History = draft.History
                };
                toolsCfg = draft.Embedding.Tools;
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dt = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = new EmbeddingConfig
                            {
                                Enabled = cur.Embedding?.Enabled ?? true,
                                TopK = cur.Embedding?.TopK ?? 5,
                                MaxContextChars = cur.Embedding?.MaxContextChars ?? 2000,
                                Tools = new EmbeddingToolsConfig
                                {
                                    Mode = cur.Embedding?.Tools?.Mode ?? "Classic",
                                    Top1Threshold = dt.Top1Threshold,
                                    LightningTop1Threshold = dt.LightningTop1Threshold,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = dt.ScoreWeights ?? new EmbeddingToolsScoreWeights { Name = 0.6, Description = 0.4 },
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            },
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘阈值与权重’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
                    }
                    catch (System.Exception ex)
                    {
                        Verse.Messages.Message("RimAI: 应用失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                    }
                },
                resetLabel: "重置本区设置",
                onReset: () =>
                {
                    var currentTools = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
                    draft = new CoreConfig
                    {
                        LLM = draft.LLM,
                        EventAggregator = draft.EventAggregator,
                        Orchestration = draft.Orchestration,
                        Embedding = new EmbeddingConfig
                        {
                            Enabled = draft?.Embedding?.Enabled ?? true,
                            TopK = draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = currentTools?.Mode ?? "Classic",
                                Top1Threshold = 0.82,
                                LightningTop1Threshold = 0.86,
                                IndexPath = currentTools?.IndexPath ?? "auto",
                                AutoBuildOnStart = currentTools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = currentTools?.BlockDuringBuild ?? true,
                                ScoreWeights = new EmbeddingToolsScoreWeights { Name = 0.6, Description = 0.4 },
                                DynamicThresholds = currentTools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        },
                        History = draft.History
                    };
                });
            return draft;
        }
    }
}


