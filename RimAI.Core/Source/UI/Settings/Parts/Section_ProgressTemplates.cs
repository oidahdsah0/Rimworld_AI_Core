using System.Collections.Generic;
using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 编排 - 进度模板
    /// </summary>
    internal sealed class Section_ProgressTemplates : ISettingsSection
    {
        private string _newStageKey = string.Empty;
        private string _newStageValue = string.Empty;

        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 编排 - 进度模板");
            SettingsUIUtil.LabelWithTip(list, "进度模板（可定制）:", "占位符：{Source} / {Stage} / {Message}");
            var progressCfg = draft.Orchestration.Progress;
            var curDefaultTpl = progressCfg.DefaultTemplate ?? "";
            var rectDef = list.GetRect(Text.LineHeight);
            var newDefaultTpl = Widgets.TextField(rectDef, curDefaultTpl);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            if (!string.Equals(newDefaultTpl, curDefaultTpl))
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = draft.Orchestration.Strategy,
                        Planning = draft.Orchestration.Planning,
                        Progress = new OrchestrationProgressConfig
                        {
                            DefaultTemplate = newDefaultTpl,
                            StageTemplates = progressCfg.StageTemplates,
                            PayloadPreviewChars = progressCfg.PayloadPreviewChars
                        }
                    },
                    Embedding = draft.Embedding,
                    History = draft.History
                };
                progressCfg = draft.Orchestration.Progress;
            }

            int curPreview = progressCfg.PayloadPreviewChars;
            SettingsUIUtil.LabelWithTip(list, $"Payload 预览长度: {curPreview}", "");
            float previewF = list.Slider(curPreview, 0, 2000);
            int newPreview = Mathf.Clamp(Mathf.RoundToInt(previewF), 0, 2000);
            if (newPreview != curPreview)
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = draft.Orchestration.Strategy,
                        Planning = draft.Orchestration.Planning,
                        Progress = new OrchestrationProgressConfig
                        {
                            DefaultTemplate = progressCfg.DefaultTemplate,
                            StageTemplates = progressCfg.StageTemplates,
                            PayloadPreviewChars = newPreview
                        }
                    },
                    Embedding = draft.Embedding,
                    History = draft.History
                };
                progressCfg = draft.Orchestration.Progress;
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var pr = draft?.Orchestration?.Progress ?? new OrchestrationProgressConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = new OrchestrationConfig
                            {
                                Strategy = cur.Orchestration?.Strategy ?? "Classic",
                                Planning = cur.Orchestration?.Planning ?? new PlanningConfig(),
                                Progress = new OrchestrationProgressConfig
                                {
                                    DefaultTemplate = pr.DefaultTemplate,
                                    StageTemplates = pr.StageTemplates,
                                    PayloadPreviewChars = pr.PayloadPreviewChars
                                }
                            },
                            Embedding = cur.Embedding,
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘进度模板’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                        Orchestration = new OrchestrationConfig
                        {
                            Strategy = draft.Orchestration.Strategy,
                            Planning = draft.Orchestration.Planning,
                            Progress = new OrchestrationProgressConfig
                            {
                                DefaultTemplate = "[${Source}] ${Stage}: ${Message}".Replace("${","{").Replace("}","}"),
                                StageTemplates = new Dictionary<string, string>(),
                                PayloadPreviewChars = 200
                            }
                        },
                        Embedding = draft.Embedding,
                        History = draft.History
                    };
                });

            // 常用 Stage 模板快捷编辑
            var commonStages = new[] { "ToolMatch", "Planner", "FinalPrompt" };
            foreach (var stageKey in commonStages)
            {
                SettingsUIUtil.LabelWithTip(list, $"Stage 模板：{stageKey}", $"仅应用于 Stage={stageKey} 的进度事件。");
                progressCfg = draft.Orchestration.Progress;
                string cur = null;
                if (progressCfg.StageTemplates != null && progressCfg.StageTemplates.TryGetValue(stageKey, out var v)) cur = v;
                var rect = list.GetRect(Text.LineHeight);
                var newTpl = Widgets.TextField(rect, cur ?? string.Empty);
                list.Gap(SettingsUIUtil.UIControlSpacing);
                if (!string.Equals(cur ?? string.Empty, newTpl))
                {
                    var dict = new Dictionary<string, string>(progressCfg.StageTemplates ?? new Dictionary<string, string>());
                    if (string.IsNullOrWhiteSpace(newTpl)) dict.Remove(stageKey); else dict[stageKey] = newTpl;
                    draft = new CoreConfig
                    {
                        LLM = draft.LLM,
                        EventAggregator = draft.EventAggregator,
                        Orchestration = new OrchestrationConfig
                        {
                            Strategy = draft.Orchestration.Strategy,
                            Planning = draft.Orchestration.Planning,
                            Progress = new OrchestrationProgressConfig
                            {
                                DefaultTemplate = progressCfg.DefaultTemplate,
                                StageTemplates = dict,
                                PayloadPreviewChars = progressCfg.PayloadPreviewChars
                            }
                        },
                        Embedding = draft.Embedding,
                        History = draft.History
                    };
                }
            }

            // 自定义Stage模板添加/更新
            SettingsUIUtil.LabelWithTip(list, "自定义 Stage 模板:", "左侧填入 Stage 名称，右侧填入模板文本；点击添加/更新。留空模板可删除对应键。");
            var row = list.GetRect(Text.LineHeight);
            float labelW = 80f;
            float half = row.width / 2f;
            var keyLabel = new Rect(row.x, row.y, labelW, row.height);
            Widgets.Label(keyLabel, "Stage 名称");
            var keyBox = new Rect(row.x + labelW, row.y, half - labelW - 8f, row.height);
            _newStageKey = Widgets.TextField(keyBox, _newStageKey ?? string.Empty);
            var valLabel = new Rect(row.x + half, row.y, labelW, row.height);
            Widgets.Label(valLabel, "模板");
            var valBox = new Rect(row.x + half + labelW, row.y, half - labelW - 8f, row.height);
            _newStageValue = Widgets.TextField(valBox, _newStageValue ?? string.Empty);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            var actionRow = list.GetRect(32f);
            float halfW = (actionRow.width - SettingsUIUtil.UIControlSpacing) / 2f;
            var addBtnRect = new Rect(actionRow.x, actionRow.y, halfW, actionRow.height);
            var saveBtnRect = new Rect(actionRow.x + halfW + SettingsUIUtil.UIControlSpacing, actionRow.y, halfW, actionRow.height);

            if (Widgets.ButtonText(addBtnRect, "添加/更新 Stage 模板"))
            {
                if (!string.IsNullOrWhiteSpace(_newStageKey))
                {
                    var dict = new Dictionary<string, string>(draft.Orchestration.Progress.StageTemplates ?? new Dictionary<string, string>());
                    if (string.IsNullOrWhiteSpace(_newStageValue)) dict.Remove(_newStageKey); else dict[_newStageKey] = _newStageValue;
                    draft = new CoreConfig
                    {
                        LLM = draft.LLM,
                        EventAggregator = draft.EventAggregator,
                        Orchestration = new OrchestrationConfig
                        {
                            Strategy = draft.Orchestration.Strategy,
                            Planning = draft.Orchestration.Planning,
                            Progress = new OrchestrationProgressConfig
                            {
                                DefaultTemplate = draft.Orchestration.Progress.DefaultTemplate,
                                StageTemplates = dict,
                                PayloadPreviewChars = draft.Orchestration.Progress.PayloadPreviewChars
                            }
                        },
                        Embedding = draft.Embedding,
                        History = draft.History
                    };
                }
            }

            if (Widgets.ButtonText(saveBtnRect, "保存并应用"))
            {
                try
                {
                    var config = CoreServices.Locator.Get<IConfigurationService>();
                    config.Apply(draft);
                    Verse.Messages.Message("RimAI: 设置已应用", RimWorld.MessageTypeDefOf.PositiveEvent, historical: false);
                }
                catch (System.Exception ex)
                {
                    Verse.Messages.Message("RimAI: 应用设置失败 - " + ex.Message, RimWorld.MessageTypeDefOf.RejectInput, historical: false);
                }
            }
            return draft;
        }
    }
}


