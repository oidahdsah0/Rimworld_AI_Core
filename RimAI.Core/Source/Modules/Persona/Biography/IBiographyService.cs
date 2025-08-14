using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona.Biography
{
	internal interface IBiographyService
	{
		IReadOnlyList<RimAI.Core.Source.Modules.Persona.BiographyItem> List(string entityId);
		void Upsert(string entityId, RimAI.Core.Source.Modules.Persona.BiographyItem item);
		void Remove(string entityId, string id);
		Task<List<RimAI.Core.Source.Modules.Persona.BiographyItem>> GenerateDraftAsync(string entityId, CancellationToken ct = default);
	}
}


