using System.Collections.Generic;

namespace RimAI.Core.Modules.Prompting
{
    internal sealed class PromptTemplate
    {
        public Dictionary<string, List<string>> Templates { get; set; } = new Dictionary<string, List<string>>();
        public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
        public int Version { get; set; } = 1;
        public string Locale { get; set; } = "zh-Hans";
    }

    internal interface IPromptTemplateService
    {
        /// <summary>
        /// 解析当前应使用的语言（UseGameLanguage=true 时优先使用游戏语言，否则使用配置 Locale）。
        /// </summary>
        string ResolveLocale();

        /// <summary>
        /// 获取当前语言对应的模板（自动合并母版与用户覆盖）。
        /// </summary>
        PromptTemplate Get(string desiredLocale = null);
    }
}


