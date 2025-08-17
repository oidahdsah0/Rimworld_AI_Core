using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona.Job
{
	internal interface IPersonaJobService
	{
		RimAI.Core.Source.Modules.Persona.PersonaJobSnapshot Get(string entityId);
		void Set(string entityId, string name, string description);
	}
}


