using System;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Orchestration
{
    /// <summary>
    /// 组装模式（输入驱动）。
    /// </summary>
    internal enum PromptMode
    {
        Chat,
        Command,
        Stage
    }

    /// <summary>
    /// 信念/意识形态片段（按 P10.5 约定的四段文本）。
    /// </summary>
    internal sealed class BeliefsModel
    {
        public string Worldview { get; set; }
        public string Values { get; set; }
        public string CodeOfConduct { get; set; }
        public string TraitsText { get; set; }
    }

    /// <summary>
    /// 提示组装输入（由 Prompt Organizer 构造）。
    /// </summary>
    internal sealed class PromptAssemblyInput
    {
        public PromptMode Mode { get; set; } = PromptMode.Chat;
        public string Locale { get; set; }

        // Persona 与个性化
        public string PersonaSystemPrompt { get; set; }
        public BeliefsModel Beliefs { get; set; }
        public List<string> BiographyParagraphs { get; set; } = new List<string>();

        // 会话/场景
        public string FixedPromptOverride { get; set; }

        // 历史与上下文
        public List<string> RecapSegments { get; set; } = new List<string>();
        public List<string> HistorySnippets { get; set; } = new List<string>();
        public List<string> WorldFacts { get; set; } = new List<string>();
        public List<string> StageHistory { get; set; } = new List<string>();

        // 工具与附加
        public List<string> ToolResults { get; set; } = new List<string>();
        public List<string> Extras { get; set; } = new List<string>();

        // 预算
        public int? MaxPromptChars { get; set; }
    }
}


