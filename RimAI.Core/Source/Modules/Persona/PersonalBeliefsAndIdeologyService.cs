using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RimAI.Core.Modules.Persona
{
    /// <summary>
    /// 个人观点与意识形态服务（线程安全内存实现）。
    /// </summary>
    internal sealed class PersonalBeliefsAndIdeologyService : IPersonalBeliefsAndIdeologyService
    {
        private readonly ConcurrentDictionary<string, PersonalBeliefs> _byPawn = new();

        public PersonalBeliefs GetByPawn(string pawnId)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return new PersonalBeliefs("", "", "", "");
            return _byPawn.TryGetValue(pawnId, out var v) ? v : new PersonalBeliefs("", "", "", "");
        }

        public void UpsertByPawn(string pawnId, PersonalBeliefs beliefs)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return;
            _byPawn[pawnId] = beliefs ?? new PersonalBeliefs("", "", "", "");
        }

        public bool DeleteByPawn(string pawnId)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return false;
            return _byPawn.TryRemove(pawnId, out _);
        }

        public IReadOnlyDictionary<string, PersonalBeliefs> ExportSnapshot()
        {
            return new Dictionary<string, PersonalBeliefs>(_byPawn);
        }

        public void ImportSnapshot(IReadOnlyDictionary<string, PersonalBeliefs> snapshot)
        {
            _byPawn.Clear();
            if (snapshot == null) return;
            foreach (var kv in snapshot)
            {
                _byPawn[kv.Key] = kv.Value ?? new PersonalBeliefs("", "", "", "");
            }
        }
    }
}


