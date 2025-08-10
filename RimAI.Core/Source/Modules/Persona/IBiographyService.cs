using System;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 人物传记（段落型字典）服务（V2）。仅在 1v1 player↔pawn 场景使用。
    /// </summary>
    internal interface IBiographyService
    {
        // 按 pawnId（单体）存取
        IReadOnlyList<BiographyItem> ListByPawn(string pawnId);
        BiographyItem Add(string pawnId, string text);
        bool Update(string pawnId, string itemId, string newText);
        bool Remove(string pawnId, string itemId);
        bool Reorder(string pawnId, string itemId, int newIndex);

        // 快照（持久化）
        IReadOnlyDictionary<string, IReadOnlyList<BiographyItem>> ExportSnapshot();
        void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyList<BiographyItem>> snapshot);
    }

    internal sealed class BiographyItem : IEquatable<BiographyItem>
    {
        public string Id { get; }
        public string Text { get; }
        public DateTime CreatedAt { get; }
        public BiographyItem(string id, string text, DateTime createdAt)
        {
            Id = id;
            Text = text;
            CreatedAt = createdAt;
        }
        public bool Equals(BiographyItem other)
        {
            if (other is null) return false;
            return Id == other.Id && Text == other.Text && CreatedAt == other.CreatedAt;
        }
    }
}


