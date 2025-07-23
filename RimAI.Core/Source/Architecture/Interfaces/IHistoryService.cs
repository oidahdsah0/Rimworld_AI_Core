using System.Collections.Generic;
using RimAI.Core.Architecture.Models;

namespace RimAI.Core.Architecture.Interfaces
{
    public interface IHistoryService : IPersistable
    {
        /// <summary>
        /// 为一组参与者开始或获取一个对话ID。
        /// 如果是新对话，会自动创建并更新索引。
        /// </summary>
        /// <param name="participantIds">参与对话的所有实体ID列表。</param>
        /// <returns>这场对话的唯一ID (ConversationId)。</returns>
        string StartOrGetConversation(List<string> participantIds);

        /// <summary>
        /// 向指定的对话中添加一条记录。
        /// </summary>
        /// <param name="conversationId">对话ID。</param>
        /// <param name="entry">包含游戏时间戳的对话条目。</param>
        void AddEntry(string conversationId, ConversationEntry entry);

        /// <summary>
        /// 获取一个结构化的历史上下文，区分主线对话和附加参考对话。
        /// </summary>
        /// <param name="primaryParticipants">当前对话的直接参与者ID列表。</param>
        /// <param name="limit">每个历史列表的记录条数上限。</param>
        /// <returns>一个包含主次历史的结构化对象。</returns>
        HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10);
    }
} 