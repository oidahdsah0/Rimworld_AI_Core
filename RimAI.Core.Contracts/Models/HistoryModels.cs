using System;
using System.Collections.Generic;

namespace RimAI.Core.Contracts.Models
{
    /// <summary>
    /// 单条对话记录。
    /// </summary>
    public sealed class ConversationEntry
    {
        public string SpeakerId { get; }
        public string Content { get; }
        public DateTime Timestamp { get; }

        public ConversationEntry(string speakerId, string content, DateTime timestamp)
        {
            SpeakerId = speakerId;
            Content = content;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// 一组对话条目的集合，表示一个会话账本。
    /// </summary>
    public sealed class Conversation
    {
        public IReadOnlyList<ConversationEntry> Entries { get; }

        public Conversation(IReadOnlyList<ConversationEntry> entries)
        {
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }
    }

    /// <summary>
    /// HistoryService 用于持久化的快照数据结构，仅包含主存储字典与倒排索引。
    /// 该类型不可变，确保序列化过程中的线程安全。
    /// </summary>
    /// <summary>
    /// 查询历史时返回的上下文结果。
    /// </summary>
    public sealed class HistoricalContext
    {
        public IReadOnlyList<Conversation> MainHistory { get; }
        public IReadOnlyList<Conversation> BackgroundHistory { get; }

        public HistoricalContext(
            IReadOnlyList<Conversation> mainHistory,
            IReadOnlyList<Conversation> backgroundHistory)
        {
            MainHistory = mainHistory ?? throw new ArgumentNullException(nameof(mainHistory));
            BackgroundHistory = backgroundHistory ?? throw new ArgumentNullException(nameof(backgroundHistory));
        }
    }

    public sealed class HistoryState
    {
        /// <summary>
        /// 主存储：会话ID => Conversation
        /// </summary>
        public IReadOnlyDictionary<string, Conversation> PrimaryStore { get; }

        /// <summary>
        /// 倒排索引：参与者ID => 该参与者出现的所有会话ID集合
        /// </summary>
        public IReadOnlyDictionary<string, HashSet<string>> InvertedIndex { get; }

        public HistoryState(
            IReadOnlyDictionary<string, Conversation> primaryStore,
            IReadOnlyDictionary<string, HashSet<string>> invertedIndex)
        {
            PrimaryStore = primaryStore ?? throw new ArgumentNullException(nameof(primaryStore));
            InvertedIndex = invertedIndex ?? throw new ArgumentNullException(nameof(invertedIndex));
        }
    }
}
