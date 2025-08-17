using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.LLM;

namespace RimAI.Core.Source.Modules.Persona.Job
{
	internal sealed class PersonaJobService : IPersonaJobService
	{
		private readonly ILLMService _llm;
		private readonly RimAI.Core.Source.Modules.Persona.IPersonaService _persona;

		public PersonaJobService(ILLMService llm, RimAI.Core.Source.Modules.Persona.IPersonaService persona)
		{
			_llm = llm;
			_persona = persona;
		}

		public RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot Get(string entityId)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return new RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot();
			var rec = _persona.Get(entityId) ?? new RimAI.Core.Source.Modules.Persona.PersonaRecordSnapshot { EntityId = entityId };
			return rec.Job ?? new RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot();
		}

		public void Set(string entityId, string name, string description)
		{
			if (string.IsNullOrWhiteSpace(entityId)) return;
			_persona.Upsert(entityId, editor => editor.SetJob(name ?? string.Empty, description ?? string.Empty));
		}
	}
}


