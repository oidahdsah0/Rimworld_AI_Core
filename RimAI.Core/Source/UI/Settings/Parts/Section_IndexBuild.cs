using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Embedding;
using RimAI.Core.Settings;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 向量索引 - 构建与阻断
    /// </summary>
    internal sealed class Section_IndexBuild : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 向量索引 - 构建与阻断");
            SettingsUIUtil.LabelWithTip(list, "向量索引构建:", "控制工具向量索引的自动构建与阻断策略。");
            var toolsCfg = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();

            bool autoBuild = toolsCfg?.AutoBuildOnStart ?? true;
            list.CheckboxLabeled("启动时自动构建索引", ref autoBuild);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            bool blockDuring = toolsCfg?.BlockDuringBuild ?? true;
            list.CheckboxLabeled("构建期间阻断工具调用", ref blockDuring);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            if (autoBuild != toolsCfg.AutoBuildOnStart || blockDuring != toolsCfg.BlockDuringBuild)
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
                            AutoBuildOnStart = autoBuild,
                            BlockDuringBuild = blockDuring,
                            ScoreWeights = toolsCfg?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
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
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = dt.AutoBuildOnStart,
                                    BlockDuringBuild = dt.BlockDuringBuild,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            },
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘构建与阻断’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                                AutoBuildOnStart = true,
                                BlockDuringBuild = true,
                                ScoreWeights = currentTools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
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


