using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Source.Modules.Persona
{
	internal interface IPersonaService
	{
		PersonaRecordSnapshot Get(string entityId);
		void Upsert(string entityId, Action<PersonaRecordEditor> edit);
		void Delete(string entityId);

		string ComposePersonaBlock(string entityId, PersonaComposeOptions options, out PersonaComposeAudit audit);

		event Action<string, string[]> OnPersonaUpdated;
	}

	internal sealed class PersonaRecordSnapshot
	{
		public string EntityId;
		public PersonaJobSnapshot Job;
		public List<BiographyItem> Biography;
		public IdeologySnapshot Ideology;
		public FixedPromptSnapshot FixedPrompts;
		public string Locale;
	}

	internal sealed class PersonaJobSnapshot { public string Name; public string Description; public System.DateTime UpdatedAtUtc; }
	internal sealed class BiographyItem { public string Id; public string Text; public string Source; public System.DateTime UpdatedAtUtc; }
	internal sealed class IdeologySnapshot { public string Worldview; public string Values; public string CodeOfConduct; public string TraitsText; public System.DateTime UpdatedAtUtc; }
	internal sealed class FixedPromptSnapshot { public string Text; public System.DateTime UpdatedAtUtc; }

	internal sealed class PersonaRecordEditor
	{
		public Action<string, string> SetJobAction;
		public Action<string> SetFixedPromptAction;
		public Action<BiographyItem> UpsertBiographyAction;
		public Action<string> RemoveBiographyAction;
		public Action<IdeologySnapshot> SetIdeologyAction;

		public void SetJob(string name, string description) => SetJobAction?.Invoke(name, description);
		public void SetFixedPrompt(string text) => SetFixedPromptAction?.Invoke(text);
		public void AddOrUpdateBiography(string id, string text, string source)
		{
			UpsertBiographyAction?.Invoke(new BiographyItem { Id = id, Text = text, Source = source, UpdatedAtUtc = System.DateTime.UtcNow });
		}
		public void RemoveBiography(string id) => RemoveBiographyAction?.Invoke(id);
		public void SetIdeology(string worldview, string values, string codeOfConduct, string traitsText)
		{
			SetIdeologyAction?.Invoke(new IdeologySnapshot { Worldview = worldview, Values = values, CodeOfConduct = codeOfConduct, TraitsText = traitsText, UpdatedAtUtc = System.DateTime.UtcNow });
		}
	}

	internal sealed class PersonaComposeOptions
	{
		public string Locale = "zh-Hans";
		public int MaxTotalChars = 4000;
		public int MaxJobChars = 600, MaxFixedChars = 800, MaxIdeologySegment = 600, MaxBioPerItem = 400, MaxBioItems = 4;
		public bool IncludeJob = true, IncludeFixedPrompts = true, IncludeIdeology = true, IncludeBiography = true;
	}

	internal sealed class PersonaComposeAudit { public int TotalChars; public List<(string seg, int len, bool truncated)> Segments = new(); }
}


