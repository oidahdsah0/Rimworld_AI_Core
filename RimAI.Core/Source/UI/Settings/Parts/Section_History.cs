using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    /// <summary>
    /// 历史 / 前情提要 分节
    /// </summary>
    internal sealed class Section_History : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 历史 / 前情提要");

            var h = draft?.History ?? new HistoryConfig();
            int curN = h.SummaryEveryNRounds;
            SettingsUIUtil.LabelWithTip(list, $"每 N 轮自动总结: {curN}", "达到 N 轮时触发一次非流式总结。");
            int newN = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curN, 1, 20)), 1, 20);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curTen = h.RecapUpdateEveryRounds;
            SettingsUIUtil.LabelWithTip(list, $"每 10 轮叠加（阈值）: {curTen}", "达到该轮次阈值时叠加到前情提要（建议 10）。");
            int newTen = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curTen, 5, 20)), 5, 20);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curMaxEntries = h.RecapDictMaxEntries;
            SettingsUIUtil.LabelWithTip(list, $"前情提要最大条目数: {curMaxEntries}", "1=仅保留最新；0/负数=无限。");
            int newMaxEntries = Mathf.RoundToInt(list.Slider(curMaxEntries, -1, 100));
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curMaxChars = h.RecapMaxChars;
            SettingsUIUtil.LabelWithTip(list, $"单条前情提要最大长度: {curMaxChars}", "每条总结/叠加的最大字符数，超出将裁剪。");
            int newMaxChars = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curMaxChars, 200, 3000)), 200, 3000);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curPage = h.HistoryPageSize;
            SettingsUIUtil.LabelWithTip(list, $"历史分页大小: {curPage}", "历史管理窗体分页大小（M3 生效）。");
            int newPage = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curPage, 50, 500)), 50, 500);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curPrompt = h.MaxPromptChars;
            SettingsUIUtil.LabelWithTip(list, $"提示组装总长度预算: {curPrompt}", "注入到 system 提示的总长度预算（M4 生效）。");
            int newPrompt = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curPrompt, 1000, 8000)), 1000, 8000);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            int curUndo = h.UndoWindowSeconds;
            SettingsUIUtil.LabelWithTip(list, $"删除撤销窗口(秒): {curUndo}", "历史删除后的可撤销窗口时长，0=不提供撤销。");
            int newUndo = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curUndo, 0, 15)), 0, 15);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            var b = h.Budget ?? new HistoryBudgetConfig();
            int curLatency = b.MaxLatencyMs;
            SettingsUIUtil.LabelWithTip(list, $"单次总结最大延迟(ms): {curLatency}", "LLM 总结/叠加调用的超时上限。");
            int newLatency = Mathf.Clamp(Mathf.RoundToInt(list.Slider(curLatency, 1000, 20000)), 1000, 20000);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            double curBudget = b.MonthlyBudgetUSD;
            SettingsUIUtil.LabelWithTip(list, $"月度预算(USD): {curBudget:F2}", "仅提示用途。M2 不做硬限制。");
            float budgetF = list.Slider((float)curBudget, 0f, 50f);
            double newBudget = System.Math.Round(budgetF, 2);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            bool changed = newN != curN || newTen != curTen || newMaxEntries != curMaxEntries ||
                           newMaxChars != curMaxChars || newPage != curPage || newPrompt != curPrompt ||
                           newLatency != curLatency || System.Math.Abs(newBudget - curBudget) > 0.0001 || newUndo != curUndo;

            if (changed)
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = draft.Embedding,
                        History = new HistoryConfig
                        {
                            SummaryEveryNRounds = newN,
                            RecapUpdateEveryRounds = newTen,
                            RecapDictMaxEntries = newMaxEntries,
                            RecapMaxChars = newMaxChars,
                            HistoryPageSize = newPage,
                            MaxPromptChars = newPrompt,
                            UndoWindowSeconds = newUndo,
                            Budget = new HistoryBudgetConfig
                            {
                                MaxLatencyMs = newLatency,
                                MonthlyBudgetUSD = newBudget
                            }
                        }
                };
            }

            SettingsUIUtil.DrawSaveResetRow(list, "保存本区设置",
                onSave: () =>
                {
                    try
                    {
                        var config = CoreServices.Locator.Get<IConfigurationService>();
                        var cur = config.Current;
                        var d = draft.History ?? new HistoryConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = cur.Embedding,
                            History = new HistoryConfig
                            {
                                SummaryEveryNRounds = d.SummaryEveryNRounds,
                                RecapUpdateEveryRounds = d.RecapUpdateEveryRounds,
                                RecapDictMaxEntries = d.RecapDictMaxEntries,
                                RecapMaxChars = d.RecapMaxChars,
                                HistoryPageSize = d.HistoryPageSize,
                                MaxPromptChars = d.MaxPromptChars,
                                UndoWindowSeconds = d.UndoWindowSeconds,
                                Budget = new HistoryBudgetConfig
                                {
                                    MaxLatencyMs = d.Budget?.MaxLatencyMs ?? 5000,
                                    MonthlyBudgetUSD = d.Budget?.MonthlyBudgetUSD ?? 5.0
                                }
                            }
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘历史/前情提要’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                        Embedding = draft.Embedding,
                        History = new HistoryConfig()
                    };
                });
            return draft;
        }
    }
}


