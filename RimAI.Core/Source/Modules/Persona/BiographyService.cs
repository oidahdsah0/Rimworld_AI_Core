using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 人物传记服务的内存实现（线程安全）。
    /// </summary>
    internal sealed class BiographyService : IBiographyService
    {
        private readonly ConcurrentDictionary<string, List<BiographyItem>> _store = new();

        public IReadOnlyList<BiographyItem> ListByPawn(string pawnId)
        {
            var k = (pawnId ?? string.Empty).Trim();
            if (_store.TryGetValue(k, out var list))
            {
                lock (list)
                {
                    return list.ToList();
                }
            }
            return Array.Empty<BiographyItem>();
        }

        public BiographyItem Add(string pawnId, string text)
        {
            var k = (pawnId ?? string.Empty).Trim();
            var item = new BiographyItem(Guid.NewGuid().ToString("N"), text ?? string.Empty, DateTime.UtcNow);
            var list = _store.GetOrAdd(k, _ => new List<BiographyItem>());
            lock (list)
            {
                list.Add(item);
                return item;
            }
        }

        public bool Update(string pawnId, string itemId, string newText)
        {
            var k = (pawnId ?? string.Empty).Trim();
            if (!_store.TryGetValue(k, out var list)) return false;
            lock (list)
            {
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                var old = list[idx];
                list[idx] = new BiographyItem(old.Id, newText ?? string.Empty, old.CreatedAt);
                return true;
            }
        }

        public bool Remove(string pawnId, string itemId)
        {
            var k = (pawnId ?? string.Empty).Trim();
            if (!_store.TryGetValue(k, out var list)) return false;
            lock (list)
            {
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                list.RemoveAt(idx);
                return true;
            }
        }

        public bool Reorder(string pawnId, string itemId, int newIndex)
        {
            var k = (pawnId ?? string.Empty).Trim();
            if (!_store.TryGetValue(k, out var list)) return false;
            lock (list)
            {
                var idx = list.FindIndex(x => x.Id == itemId);
                if (idx < 0) return false;
                newIndex = Math.Max(0, Math.Min(newIndex, list.Count - 1));
                var item = list[idx];
                list.RemoveAt(idx);
                list.Insert(newIndex, item);
                return true;
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyList<BiographyItem>> ExportSnapshot()
        {
            return _store.ToDictionary(k => k.Key, v => (IReadOnlyList<BiographyItem>)v.Value.ToList());
        }

        public void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyList<BiographyItem>> snapshot)
        {
            _store.Clear();
            if (snapshot == null) return;
            foreach (var kvp in snapshot)
            {
                _store[kvp.Key] = kvp.Value?.ToList() ?? new List<BiographyItem>();
            }
        }
    }
}


