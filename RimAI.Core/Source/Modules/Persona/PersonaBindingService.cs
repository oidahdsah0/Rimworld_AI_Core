using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RimAI.Core.Modules.Persona
{
    internal sealed class PersonaBinding
    {
        public string PawnId { get; set; }
        public string PersonaName { get; set; }
        public int Revision { get; set; }
    }

    internal interface IPersonaBindingService
    {
        bool Bind(string pawnId, string personaName, int revision = 0);
        bool Unbind(string pawnId);
        PersonaBinding GetBinding(string pawnId);
        IReadOnlyList<PersonaBinding> GetAllBindings();
    }

    internal sealed class PersonaBindingService : IPersonaBindingService
    {
        private readonly ConcurrentDictionary<string, PersonaBinding> _bindings = new ConcurrentDictionary<string, PersonaBinding>(StringComparer.Ordinal);

        public bool Bind(string pawnId, string personaName, int revision = 0)
        {
            if (string.IsNullOrWhiteSpace(pawnId) || string.IsNullOrWhiteSpace(personaName)) return false;
            var binding = new PersonaBinding { PawnId = pawnId, PersonaName = personaName, Revision = revision };
            _bindings[pawnId] = binding;
            return true;
        }

        public bool Unbind(string pawnId)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return false;
            return _bindings.TryRemove(pawnId, out _);
        }

        public PersonaBinding GetBinding(string pawnId)
        {
            if (string.IsNullOrWhiteSpace(pawnId)) return null;
            _bindings.TryGetValue(pawnId, out var b);
            return b;
        }

        public IReadOnlyList<PersonaBinding> GetAllBindings()
        {
            return _bindings.Values.OrderBy(b => b.PawnId, StringComparer.Ordinal).ToList();
        }
    }
}


