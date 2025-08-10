using System;
using System.Linq;

namespace RimAI.Core.UI.HistoryManager
{
    internal static class HistoryUIState
    {
        // 全局记忆当前窗口上下文（简单静态变量实现即可，避免频繁传参）
        public static string CurrentConvKey { get; set; } = string.Empty;
        public static string CurrentConversationId { get; set; } = string.Empty;

        public static string CanonicalizeConvKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var ids = key.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim());
            return string.Join("|", ids.OrderBy(x => x, StringComparer.Ordinal));
        }
    }
}


