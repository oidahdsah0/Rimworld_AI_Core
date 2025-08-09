using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 固定提示词服务的内存实现（线程安全）。
    /// </summary>
    internal sealed class FixedPromptService : IFixedPromptService
    {
        // convKey => (participantId => text)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _byConv = new();
        private readonly ConcurrentDictionary<string, string> _legacy = new();

        public string Get(string convKey, string participantId)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(participantId)) return string.Empty;
            if (_byConv.TryGetValue(convKey, out var map) && map.TryGetValue(participantId, out var text)) return text;
            return string.Empty;
        }

        public void Upsert(string convKey, string participantId, string text)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(participantId)) return;
            var map = _byConv.GetOrAdd(convKey, _ => new ConcurrentDictionary<string, string>());
            map[participantId] = text ?? string.Empty;
        }

        public bool Delete(string convKey, string participantId)
        {
            if (string.IsNullOrWhiteSpace(convKey) || string.IsNullOrWhiteSpace(participantId)) return false;
            if (_byConv.TryGetValue(convKey, out var map))
            {
                return map.TryRemove(participantId, out _);
            }
            return false;
        }

        public IReadOnlyDictionary<string, string> GetAll(string convKey)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return new Dictionary<string, string>();
            if (_byConv.TryGetValue(convKey, out var map)) return new Dictionary<string, string>(map);
            return new Dictionary<string, string>();
        }

        // 兼容旧签名（全局作用域）
        public string Get(string participantId) => _legacy.TryGetValue(participantId ?? string.Empty, out var text) ? text : string.Empty;
        public void Upsert(string participantId, string text) { if (!string.IsNullOrWhiteSpace(participantId)) _legacy[participantId] = text ?? string.Empty; }
        public bool Delete(string participantId) => !string.IsNullOrWhiteSpace(participantId) && _legacy.TryRemove(participantId, out _);
        public IReadOnlyDictionary<string, string> GetAll() => new Dictionary<string, string>(_legacy);

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ExportSnapshot()
        {
            var dict = new Dictionary<string, IReadOnlyDictionary<string, string>>();
            foreach (var kvp in _byConv)
            {
                var inner = new Dictionary<string, string>();
                foreach (var p in kvp.Value)
                {
                    inner[p.Key] = p.Value;
                }
                dict[kvp.Key] = inner;
            }
            return dict;
        }

        public void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> snapshot)
        {
            _byConv.Clear();
            if (snapshot == null) return;
            foreach (var kvp in snapshot)
            {
                var inner = new ConcurrentDictionary<string, string>();
                if (kvp.Value != null)
                {
                    foreach (var p in kvp.Value)
                    {
                        inner[p.Key] = p.Value;
                    }
                }
                _byConv[kvp.Key] = inner;
            }
        }
    }
}


