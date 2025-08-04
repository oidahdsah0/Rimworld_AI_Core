using System.Collections.Generic;

namespace RimAI.Core.Contracts.Data
{
    /// <summary>
    /// 可被 Scribe 直接序列化的 HistoryService 状态快照。
    /// </summary>
    public class HistoryStateSnapshot
    {
        public List<Conversation> Conversations { get; set; } = new();
    }
}
