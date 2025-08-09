using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Embedding;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 工具匹配 - 动态阈值
    /// </summary>
    internal sealed class Section_DynamicThresholds : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 工具匹配 - 动态阈值");
            var toolsCfg = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
            var dyn = toolsCfg?.DynamicThresholds ?? new EmbeddingDynamicThresholds();

            SettingsUIUtil.LabelWithTip(list, "动态阈值:", "根据近期命中质量微调 Top1 阈值。");
            bool dynEnabled = dyn?.Enabled ?? true;
            list.CheckboxLabeled("启用动态阈值", ref dynEnabled);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            float dynSmoothing = (float)(dyn?.Smoothing ?? 0.2);
            SettingsUIUtil.LabelWithTip(list, $"动态阈值平滑: {dynSmoothing:F2}", "");
            dynSmoothing = list.Slider(dynSmoothing, 0f, 1f);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            float dynMin = (float)(dyn?.MinTop1 ?? 0.78);
            SettingsUIUtil.LabelWithTip(list, $"最小Top1阈值: {dynMin:F2}", "");
            dynMin = list.Slider(dynMin, 0.50f, 0.95f);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            float dynMax = (float)(dyn?.MaxTop1 ?? 0.90);
            SettingsUIUtil.LabelWithTip(list, $"最大Top1阈值: {dynMax:F2}", "");
            dynMax = list.Slider(dynMax, 0.55f, 0.99f);
            dynMax = Mathf.Max(dynMax, dynMin + 0.01f);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            if (dynEnabled != dyn.Enabled ||
                !Mathf.Approximately(dynSmoothing, (float)dyn.Smoothing) ||
                !Mathf.Approximately(dynMin, (float)dyn.MinTop1) ||
                !Mathf.Approximately(dynMax, (float)dyn.MaxTop1))
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = new EmbeddingConfig
                    {
                        Enabled = draft.Embedding.Enabled,
                        TopK = draft.Embedding.TopK,
                        MaxContextChars = draft.Embedding.MaxContextChars,
                        Tools = new EmbeddingToolsConfig
                        {
                            Mode = toolsCfg.Mode,
                            Top1Threshold = toolsCfg.Top1Threshold,
                            LightningTop1Threshold = toolsCfg.LightningTop1Threshold,
                            IndexPath = toolsCfg.IndexPath,
                            AutoBuildOnStart = toolsCfg.AutoBuildOnStart,
                            BlockDuringBuild = toolsCfg.BlockDuringBuild,
                            ScoreWeights = toolsCfg.ScoreWeights,
                            DynamicThresholds = new EmbeddingDynamicThresholds
                            {
                                Enabled = dynEnabled,
                                Smoothing = dynSmoothing,
                                MinTop1 = dynMin,
                                MaxTop1 = dynMax
                            }
                        }
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
                        var dt = draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds();
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
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = new EmbeddingDynamicThresholds
                                    {
                                        Enabled = dt.Enabled,
                                        Smoothing = dt.Smoothing,
                                        MinTop1 = dt.MinTop1,
                                        MaxTop1 = dt.MaxTop1
                                    }
                                }
                            },
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘动态阈值’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                                Top1Threshold = currentTools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = currentTools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = currentTools?.IndexPath ?? "auto",
                                AutoBuildOnStart = currentTools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = currentTools?.BlockDuringBuild ?? true,
                                ScoreWeights = currentTools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = new EmbeddingDynamicThresholds { Enabled = true, Smoothing = 0.2, MinTop1 = 0.78, MaxTop1 = 0.90 }
                            }
                        },
                        History = draft.History
                    };
                });
            return draft;
        }
    }
}


