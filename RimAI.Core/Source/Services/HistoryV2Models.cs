using System;
using System.Collections.Generic;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 内部会话记录（V2）。
    /// 主键为 ConversationId（GUID），包含参与者集合与最终输出条目列表。
    /// </summary>
    internal sealed class ConversationRecord
    {
        public string ConversationId { get; }
        public IReadOnlyList<string> ParticipantIds { get; }
        public IReadOnlyList<ConversationEntry> Entries { get; }

        public ConversationRecord(string conversationId, IReadOnlyList<string> participantIds, IReadOnlyList<ConversationEntry> entries)
        {
            ConversationId = string.IsNullOrWhiteSpace(conversationId) ? throw new ArgumentException(nameof(conversationId)) : conversationId;
            ParticipantIds = participantIds ?? throw new ArgumentNullException(nameof(participantIds));
            Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }
    }

    /// <summary>
    /// 内部历史快照（V2）。
    /// </summary>
    internal sealed class HistoryV2State
    {
        public IReadOnlyDictionary<string, ConversationRecord> Conversations { get; }
        public IReadOnlyDictionary<string, List<string>> ConvKeyIndex { get; }
        public IReadOnlyDictionary<string, List<string>> ParticipantIndex { get; }

        public HistoryV2State(
            IReadOnlyDictionary<string, ConversationRecord> conversations,
            IReadOnlyDictionary<string, List<string>> convKeyIndex,
            IReadOnlyDictionary<string, List<string>> participantIndex)
        {
            Conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
            ConvKeyIndex = convKeyIndex ?? throw new ArgumentNullException(nameof(convKeyIndex));
            ParticipantIndex = participantIndex ?? throw new ArgumentNullException(nameof(participantIndex));
        }
    }
}


