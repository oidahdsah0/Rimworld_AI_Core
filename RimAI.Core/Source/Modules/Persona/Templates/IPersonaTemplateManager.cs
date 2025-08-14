using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona.Templates
{
	internal interface IPersonaTemplateManager
	{
		Task<PersonaTemplates> GetTemplatesAsync(string locale, CancellationToken ct = default);
	}

	internal sealed class PersonaTemplates
	{
		public int Version { get; set; }
		public string Locale { get; set; }
		public PromptsSection Prompts { get; set; } = new PromptsSection();

		internal sealed class PromptsSection
		{
			public string jobFromName { get; set; }
			public string biographyDraft { get; set; }
			public string ideology { get; set; }
		}
	}
}


