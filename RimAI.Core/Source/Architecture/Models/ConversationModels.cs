using System.Collections.Generic;
using Verse;

namespace RimAI.Core.Architecture.Models
{
    /// <summary>
    /// 表示单条对话记录。
    /// </summary>
    public class ConversationEntry : IExposable
    {
        /// <summary>
        /// 发言者的唯一ID
        /// </summary>
        public string ParticipantId;

        /// <summary>
        /// 发言者的角色标签 (e.g., "user", "assistant", "character")
        /// </summary>
        public string Role;

        /// <summary>
        /// 发言内容
        /// </summary>
        public string Content;

        /// <summary>
        /// 游戏内时间戳 (Ticks)
        /// </summary>
        public long GameTicksTimestamp;

        public void ExposeData()
        {
            Scribe_Values.Look(ref ParticipantId, "participantId");
            Scribe_Values.Look(ref Role, "role");
            Scribe_Values.Look(ref Content, "content");
            Scribe_Values.Look(ref GameTicksTimestamp, "gameTicksTimestamp", 0);
        }
    }

    /// <summary>
    /// 结构化的历史上下文，用于返回给调用者，已预先分好主次。
    /// </summary>
    public class HistoricalContext
    {
        /// <summary>
        /// 主线历史：当前对话者之间的直接对话记录。
        /// </summary>
        public List<ConversationEntry> PrimaryHistory { get; set; } = new List<ConversationEntry>();

        /// <summary>
        /// 附加历史：包含了当前对话者，但也有其他人在场的对话记录。
        /// </summary>
        public List<ConversationEntry> AncillaryHistory { get; set; } = new List<ConversationEntry>();
    }
} 