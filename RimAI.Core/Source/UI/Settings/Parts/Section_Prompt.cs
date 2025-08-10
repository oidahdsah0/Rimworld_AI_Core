using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Settings.Parts
{
    internal sealed class Section_Prompt : ISettingsSection
    {
        public CoreConfig Draw(Listing_Standard list, ref int sectionIndex, CoreConfig draft)
        {
            SettingsUIUtil.SectionTitle(list, $"{sectionIndex++}. 提示词 / 模板");

            var p = draft?.Prompt ?? new PromptConfig();

            // Locale
            bool useGameLang = p.UseGameLanguage;
            SettingsUIUtil.LabelWithTip(list, $"随游戏语言: {(useGameLang ? "是" : "否")}", "若开启，将尝试随 RimWorld 当前语言选择模板文件。");
            Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), " ", ref useGameLang);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            string locale = p.Locale ?? "zh-Hans";
            SettingsUIUtil.LabelWithTip(list, $"语言代码: {locale}", "如 zh-Hans / en-US。若启用随游戏语言，则此项作为回退。");
            locale = Widgets.TextField(new Rect(0, list.CurHeight, list.ColumnWidth, 24f), locale);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            // Template keys
            string chatKey = p.TemplateChatKey ?? "chat";
            SettingsUIUtil.LabelWithTip(list, $"Chat 模板键: {chatKey}", "chat 模板段落顺序键名");
            chatKey = Widgets.TextField(new Rect(0, list.CurHeight, list.ColumnWidth, 24f), chatKey);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            string cmdKey = p.TemplateCommandKey ?? "command";
            SettingsUIUtil.LabelWithTip(list, $"Command 模板键: {cmdKey}", "command 模板段落顺序键名");
            cmdKey = Widgets.TextField(new Rect(0, list.CurHeight, list.ColumnWidth, 24f), cmdKey);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            // Paths
            string master = p.MasterPath ?? "Resources/prompts/{locale}.json";
            SettingsUIUtil.LabelWithTip(list, $"母版路径: {master}", "只读模板路径，{locale} 会在运行时替换");
            master = Widgets.TextField(new Rect(0, list.CurHeight, list.ColumnWidth, 24f), master);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            string user = p.UserOverridePath ?? "Config/RimAI/Prompts/{locale}.user.json";
            SettingsUIUtil.LabelWithTip(list, $"覆盖路径: {user}", "用户可写模板路径，{locale} 会在运行时替换");
            user = Widgets.TextField(new Rect(0, list.CurHeight, list.ColumnWidth, 24f), user);
            list.Gap(SettingsUIUtil.UIControlSpacing);

            // Chat Segments toggles
            var chatSeg = p.Segments?.Chat ?? new ChatSegments();
            SettingsUIUtil.LabelWithTip(list, "Chat 段落包含：", "控制 Chat 模板各段是否注入");
            bool chatIncPersona = chatSeg.IncludePersona; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 Persona", ref chatIncPersona); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool chatIncFP = chatSeg.IncludeFixedPrompts; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 固定提示词", ref chatIncFP); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool chatIncRecap = chatSeg.IncludeRecap; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 前情提要", ref chatIncRecap); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool chatIncRecent = chatSeg.IncludeRecentHistory; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 近期历史", ref chatIncRecent); list.Gap(SettingsUIUtil.UIControlSpacing);
            int chatRecentMax = chatSeg.RecentHistoryMaxEntries; SettingsUIUtil.LabelWithTip(list, $"近期历史 条数: {chatRecentMax}", "Chat 每次注入的近期历史条数"); chatRecentMax = Mathf.Clamp(Mathf.RoundToInt(list.Slider(chatRecentMax, 1, 20)), 1, 20); list.Gap(SettingsUIUtil.UIControlSpacing);

            // Command Segments toggles
            var cmdSeg = p.Segments?.Command ?? new CommandSegments();
            SettingsUIUtil.LabelWithTip(list, "Command 段落包含：", "控制 Command 模板各段是否注入");
            bool cmdIncPersona = cmdSeg.IncludePersona; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 Persona", ref cmdIncPersona); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool cmdIncFP = cmdSeg.IncludeFixedPrompts; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 固定提示词", ref cmdIncFP); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool cmdIncBio = cmdSeg.IncludeBiography; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 人物传记（仅1v1）", ref cmdIncBio); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool cmdIncRecap = cmdSeg.IncludeRecap; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 前情提要", ref cmdIncRecap); list.Gap(SettingsUIUtil.UIControlSpacing);
            bool cmdIncRelated = cmdSeg.IncludeRelatedHistory; Widgets.CheckboxLabeled(new Rect(0f, list.CurHeight, list.ColumnWidth, 24f), "包含 相关历史", ref cmdIncRelated); list.Gap(SettingsUIUtil.UIControlSpacing);
            int relMaxConvs = cmdSeg.RelatedMaxConversations; SettingsUIUtil.LabelWithTip(list, $"相关历史 会话数上限: {relMaxConvs}", "相关历史中纳入的会话个数上限"); relMaxConvs = Mathf.Clamp(Mathf.RoundToInt(list.Slider(relMaxConvs, 1, 10)), 1, 10); list.Gap(SettingsUIUtil.UIControlSpacing);
            int relMaxPerConv = cmdSeg.RelatedMaxEntriesPerConversation; SettingsUIUtil.LabelWithTip(list, $"相关历史 每会话条数: {relMaxPerConv}", "相关历史中每个会话采样的条数"); relMaxPerConv = Mathf.Clamp(Mathf.RoundToInt(list.Slider(relMaxPerConv, 1, 20)), 1, 20); list.Gap(SettingsUIUtil.UIControlSpacing);

            // Segment budgets
            var b = p.Budget ?? new PromptBudgetConfig();
            int persona = b.Persona; SettingsUIUtil.LabelWithTip(list, $"Persona 每段上限: {persona}", "字符数上限"); persona = Mathf.Clamp(Mathf.RoundToInt(list.Slider(persona, 200, 4000)), 200, 4000); list.Gap(SettingsUIUtil.UIControlSpacing);
            int fp = b.FixedPrompts; SettingsUIUtil.LabelWithTip(list, $"固定提示词 每段上限: {fp}", "字符数上限"); fp = Mathf.Clamp(Mathf.RoundToInt(list.Slider(fp, 200, 4000)), 200, 4000); list.Gap(SettingsUIUtil.UIControlSpacing);
            int bio = b.Biography; SettingsUIUtil.LabelWithTip(list, $"人物传记 每段上限: {bio}", "字符数上限"); bio = Mathf.Clamp(Mathf.RoundToInt(list.Slider(bio, 200, 4000)), 200, 4000); list.Gap(SettingsUIUtil.UIControlSpacing);
            int recap = b.Recap; SettingsUIUtil.LabelWithTip(list, $"前情提要 每段上限: {recap}", "字符数上限"); recap = Mathf.Clamp(Mathf.RoundToInt(list.Slider(recap, 200, 4000)), 200, 4000); list.Gap(SettingsUIUtil.UIControlSpacing);
            int rh = b.RecentHistory; SettingsUIUtil.LabelWithTip(list, $"近期历史 每段上限: {rh}", "字符数上限"); rh = Mathf.Clamp(Mathf.RoundToInt(list.Slider(rh, 200, 4000)), 200, 4000); list.Gap(SettingsUIUtil.UIControlSpacing);
            int rel = b.RelatedHistory; SettingsUIUtil.LabelWithTip(list, $"相关历史 每段上限: {rel}", "字符数上限"); rel = Mathf.Clamp(Mathf.RoundToInt(list.Slider(rel, 200, 6000)), 200, 6000); list.Gap(SettingsUIUtil.UIControlSpacing);

            bool changed = useGameLang != p.UseGameLanguage || locale != p.Locale || chatKey != p.TemplateChatKey || cmdKey != p.TemplateCommandKey || master != p.MasterPath || user != p.UserOverridePath || persona != b.Persona || fp != b.FixedPrompts || bio != b.Biography || recap != b.Recap || rh != b.RecentHistory || rel != b.RelatedHistory
                || chatIncPersona != chatSeg.IncludePersona || chatIncFP != chatSeg.IncludeFixedPrompts || chatIncRecap != chatSeg.IncludeRecap || chatIncRecent != chatSeg.IncludeRecentHistory || chatRecentMax != chatSeg.RecentHistoryMaxEntries
                || cmdIncPersona != cmdSeg.IncludePersona || cmdIncFP != cmdSeg.IncludeFixedPrompts || cmdIncBio != cmdSeg.IncludeBiography || cmdIncRecap != cmdSeg.IncludeRecap || cmdIncRelated != cmdSeg.IncludeRelatedHistory || relMaxConvs != cmdSeg.RelatedMaxConversations || relMaxPerConv != cmdSeg.RelatedMaxEntriesPerConversation;

            if (changed)
            {
                draft = new CoreConfig
                {
                    LLM = draft.LLM,
                    EventAggregator = draft.EventAggregator,
                    Orchestration = draft.Orchestration,
                    Embedding = draft.Embedding,
                    History = draft.History,
                    Prompt = new PromptConfig
                    {
                        UseGameLanguage = useGameLang,
                        Locale = locale,
                        TemplateChatKey = chatKey,
                        TemplateCommandKey = cmdKey,
                        MasterPath = master,
                        UserOverridePath = user,
                        Budget = new PromptBudgetConfig
                        {
                            Persona = persona,
                            FixedPrompts = fp,
                            Biography = bio,
                            Recap = recap,
                            RecentHistory = rh,
                            RelatedHistory = rel
                        },
                        Segments = new PromptSegmentsConfig
                        {
                            Chat = new ChatSegments
                            {
                                IncludePersona = chatIncPersona,
                                IncludeFixedPrompts = chatIncFP,
                                IncludeRecap = chatIncRecap,
                                IncludeRecentHistory = chatIncRecent,
                                RecentHistoryMaxEntries = chatRecentMax
                            },
                            Command = new CommandSegments
                            {
                                IncludePersona = cmdIncPersona,
                                IncludeFixedPrompts = cmdIncFP,
                                IncludeBiography = cmdIncBio,
                                IncludeRecap = cmdIncRecap,
                                IncludeRelatedHistory = cmdIncRelated,
                                RelatedMaxConversations = relMaxConvs,
                                RelatedMaxEntriesPerConversation = relMaxPerConv
                            }
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
                        var d = draft.Prompt ?? new PromptConfig();
                        var newCfg = new CoreConfig
                        {
                            LLM = cur.LLM,
                            EventAggregator = cur.EventAggregator,
                            Orchestration = cur.Orchestration,
                            Embedding = cur.Embedding,
                            History = cur.History,
                            Prompt = d
                        };
                        config.Apply(newCfg);
                        Verse.Messages.Message("RimAI: 已应用 ‘提示词/模板’ 设置", RimWorld.MessageTypeDefOf.TaskCompletion, historical: false);
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
                        History = draft.History,
                        Prompt = new PromptConfig()
                    };
                });
            return draft;
        }
    }
}


