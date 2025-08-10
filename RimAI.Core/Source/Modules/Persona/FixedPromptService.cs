using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 固定提示词服务的内存实现（线程安全）。
    /// </summary>
internal sealed class FixedPromptService : IFixedPromptService
    {
        // 主存：pawnId → text
        private readonly ConcurrentDictionary<string, string> _byPawn = new();
        // 覆盖层：convKey → text
        private readonly ConcurrentDictionary<string, string> _byConvOverride = new();

        // 主存（按 PawnId）
        public string GetByPawn(string pawnId)
            => string.IsNullOrWhiteSpace(pawnId) ? string.Empty : (_byPawn.TryGetValue(pawnId, out var t) ? t : string.Empty);

        public void UpsertByPawn(string pawnId, string text)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return;
            _byPawn[pawnId] = text ?? string.Empty;
        }

        public bool DeleteByPawn(string pawnId)
            => !string.IsNullOrWhiteSpace(pawnId) && _byPawn.TryRemove(pawnId, out _);

        public IReadOnlyDictionary<string, string> GetAllByPawn()
            => new Dictionary<string, string>(_byPawn);

        // 覆盖层（按 convKey）
        public string GetConvKeyOverride(string convKey)
            => string.IsNullOrWhiteSpace(convKey) ? string.Empty : (_byConvOverride.TryGetValue(convKey, out var t) ? t : string.Empty);

        public void UpsertConvKeyOverride(string convKey, string text)
        {
            if (string.IsNullOrWhiteSpace(convKey)) return;
            _byConvOverride[convKey] = text ?? string.Empty;
        }

        public bool DeleteConvKeyOverride(string convKey)
            => !string.IsNullOrWhiteSpace(convKey) && _byConvOverride.TryRemove(convKey, out _);

        public IReadOnlyDictionary<string, string> GetAllConvKeyOverrides()
            => new Dictionary<string, string>(_byConvOverride);

        // 快照（仅主存）
        public IReadOnlyDictionary<string, string> ExportSnapshot()
            => new Dictionary<string, string>(_byPawn);

        public void ImportSnapshot(IReadOnlyDictionary<string, string> snapshot)
        {
            _byPawn.Clear();
            if (snapshot == null) return;
            foreach (var kv in snapshot)
                _byPawn[kv.Key] = kv.Value ?? string.Empty;
        }
    }
}


