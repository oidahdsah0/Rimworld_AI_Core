using System;
using System.Collections.Generic;

namespace RimAI.Core.Contracts.Data
{
    /// <summary>
    /// 单条对话记录。
    /// </summary>
    public class ConversationEntry
    {
        public string Role { get; set; } = string.Empty; // "user" / "assistant"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 一组固定参与者的对话账本。
    /// </summary>
    public class Conversation
    {
        public string Id { get; set; } = string.Empty;
        public HashSet<string> Participants { get; set; } = new();
        public List<ConversationEntry> Entries { get; set; } = new();
    }

    /// <summary>
    /// 查询历史返回的上下文。
    /// </summary>
    public class HistoricalContext
    {
        public List<ConversationEntry> Mainline { get; set; } = new();
        public List<ConversationEntry> Background { get; set; } = new();
    }
}