using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 编排 - 轻量规划器（S4）
    /// </summary>
    internal sealed class Section_Planner : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 编排 - 轻量规划器（S4）");
            var planning = draft.Orchestration.Planning;
            bool enablePlan = planning.EnableLightChaining;
            list.CheckboxLabeled("启用轻量规划器", ref enablePlan);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            int maxSteps = planning.MaxSteps;
            SettingsUIUtil.LabelWithTip(list, $"MaxSteps: {maxSteps}", "");
            float stepsF = list.Slider(maxSteps, 1, 5);
            int newSteps = Mathf.Clamp(Mathf.RoundToInt(stepsF), 1, 5);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            bool allowParallel = planning.AllowParallel;
            list.CheckboxLabeled("阶段内允许只读小并发", ref allowParallel);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            int curMaxPar = planning.MaxParallelism;
            SettingsUIUtil.LabelWithTip(list, $"MaxParallelism: {curMaxPar}", "");
            float maxParF = list.Slider(curMaxPar, 1, 8);
            int newMaxPar = Mathf.Clamp(Mathf.RoundToInt(maxParF), 1, 8);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            int curFanout = planning.FanoutPerStage;
            SettingsUIUtil.LabelWithTip(list, $"Fanout/Stage: {curFanout}", "");
            float fanoutF = list.Slider(curFanout, 1, 5);
            int newFanout = Mathf.Clamp(Mathf.RoundToInt(fanoutF), 1, 5);
            list.Gap(SettingsUIUtil.UIControlSpacing);
            double curSatis = planning.SatisfactionThreshold;
            SettingsUIUtil.LabelWithTip(list, $"SatisfactionThreshold: {curSatis:F2}", "");
            float satisF = list.Slider((float)curSatis, 0.5f, 0.99f);
            double newSatis = System.Math.Round(satisF, 2);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            if (enablePlan != planning.EnableLightChaining ||
                newSteps != planning.MaxSteps ||
                allowParallel != planning.AllowParallel ||
                newMaxPar != planning.MaxParallelism ||
                newFanout != planning.FanoutPerStage ||
                System.Math.Abs(newSatis - planning.SatisfactionThreshold) > 0.0001)
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = new OrchestrationConfig
                    {
                        Strategy = draft.Orchestration.Strategy,
                        Planning = new PlanningConfig
                        {
                            EnableLightChaining = enablePlan,
                            MaxSteps = newSteps,
                            AllowParallel = allowParallel,
                            MaxParallelism = newMaxPar,
                            FanoutPerStage = newFanout,
                            SatisfactionThreshold = newSatis
                        },
                        Progress = draft.Orchestration.Progress
                    },
                    Embedding = draft.Embedding,
                    History = draft.History
                };
                planning = draft.Orchestration.Planning;
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var dp = draft?.Orchestration?.Planning ?? new PlanningConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = new OrchestrationConfig
                            {
                                Strategy = cur.Orchestration?.Strategy ?? "Classic",
                                Planning = new PlanningConfig
                                {
                                    EnableLightChaining = dp.EnableLightChaining,
                                    MaxSteps = dp.MaxSteps,
                                    AllowParallel = dp.AllowParallel,
                                    MaxParallelism = dp.MaxParallelism,
                                    FanoutPerStage = dp.FanoutPerStage,
                                    SatisfactionThreshold = dp.SatisfactionThreshold
                                },
                                Progress = cur.Orchestration?.Progress ?? new OrchestrationProgressConfig()
                            },
                            Embedding = cur.Embedding,
                            History = cur.History
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘轻量规划器’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                            Planning = new PlanningConfig
                            {
                                EnableLightChaining = false,
                                MaxSteps = 3,
                                AllowParallel = false,
                                MaxParallelism = 2,
                                FanoutPerStage = 3,
                                SatisfactionThreshold = 0.8
                            },
                            Progress = draft.Orchestration.Progress
                        },
                        Embedding = draft.Embedding,
                        History = draft.History
                    };
                });
            return draft;
        }
    }
}


