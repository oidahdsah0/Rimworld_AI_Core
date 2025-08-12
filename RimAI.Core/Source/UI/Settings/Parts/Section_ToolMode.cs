using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using RimAI.Core.Modules.Embedding;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 工具匹配 - 模式
    /// </summary>
    internal sealed class Section_ToolMode : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 工具匹配 - 模式");
            SettingsUIUtil.LabelWithTip(list,
                "工具调用模式（唯一选择）:",
                "- LightningFast: Top1≥更高阈值，工具支持快速直出，将传入 __fastResponse=true\n- FastTop1: Top1≥阈值则仅执行该工具\n- NarrowTopK: 收缩 TopK 后确定性选择第一个\n- Classic: 暴露全部工具（不使用索引）");
            var modes = new[] { "LightningFast", "FastTop1", "NarrowTopK", "Classic" };
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = modes[i];
                bool selected = string.Equals((draft?.Embedding?.Tools?.Mode) ?? "Classic", mode, System.StringComparison.OrdinalIgnoreCase);
                if (list.RadioButton(mode, selected))
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
                                Mode = mode,
                                Top1Threshold = draft?.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = draft?.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = draft?.Embedding?.Tools?.IndexPath ?? "auto",
                                AutoBuildOnStart = draft?.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = draft?.Embedding?.Tools?.BlockDuringBuild ?? true,
                                ScoreWeights = draft?.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        },
                        History = draft.History
                    };
                }
                list.Gap(SettingsUIUtil.UIControlSpacing);
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var draftTools = draft?.Embedding?.Tools ?? new EmbeddingToolsConfig();
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
                                    Mode = draftTools.Mode,
                                    Top1Threshold = cur.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                    LightningTop1Threshold = cur.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                    IndexPath = cur.Embedding?.Tools?.IndexPath ?? "auto",
                                    AutoBuildOnStart = cur.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                    BlockDuringBuild = cur.Embedding?.Tools?.BlockDuringBuild ?? true,
                                    ScoreWeights = cur.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                    DynamicThresholds = cur.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                                }
                            },
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘工具匹配-模式’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                            Enabled = draft?.Embedding?.Enabled ?? true,
                            TopK = draft?.Embedding?.TopK ?? 5,
                            MaxContextChars = draft?.Embedding?.MaxContextChars ?? 2000,
                            Tools = new EmbeddingToolsConfig
                            {
                                Mode = "FastTop1",
                                Top1Threshold = draft?.Embedding?.Tools?.Top1Threshold ?? 0.82,
                                LightningTop1Threshold = draft?.Embedding?.Tools?.LightningTop1Threshold ?? 0.86,
                                IndexPath = draft?.Embedding?.Tools?.IndexPath ?? "auto",
                                AutoBuildOnStart = draft?.Embedding?.Tools?.AutoBuildOnStart ?? true,
                                BlockDuringBuild = draft?.Embedding?.Tools?.BlockDuringBuild ?? true,
                                ScoreWeights = draft?.Embedding?.Tools?.ScoreWeights ?? new EmbeddingToolsScoreWeights(),
                                DynamicThresholds = draft?.Embedding?.Tools?.DynamicThresholds ?? new EmbeddingDynamicThresholds()
                            }
                        },
                        History = draft.History
                    };
                });
            return draft;
        }
    }
}


