using System;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 人物传记（段落型字典）服务（M3 内存 MVP）。仅在 1v1 player↔pawn 场景使用。
    /// </summary>
    internal interface IBiographyService
    {
        // 按 convKey（1v1）存取，确保以 ID 集合作为键
        IReadOnlyList<BiographyItem> List(string convKey);
        BiographyItem Add(string convKey, string text);
        bool Update(string convKey, string itemId, string newText);
        bool Remove(string convKey, string itemId);
        bool Reorder(string convKey, string itemId, int newIndex);

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


