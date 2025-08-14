using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona.Ideology
{
	internal interface IIdeologyService
	{
		RimAI.Core.Source.Modules.Persona.IdeologySnapshot Get(string entityId);
		void Set(string entityId, RimAI.Core.Source.Modules.Persona.IdeologySnapshot s);
		Task<RimAI.Core.Source.Modules.Persona.IdeologySnapshot> GenerateAsync(string entityId, CancellationToken ct = default);
	}
}


