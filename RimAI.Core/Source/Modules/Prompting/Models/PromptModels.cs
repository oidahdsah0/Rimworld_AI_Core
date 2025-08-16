namespace RimAI.Core.Source.Modules.Prompting.Models
{
	internal enum PromptScope
	{
		ChatUI,
		Stage,
		Tool
	}

	internal sealed class PromptBuildRequest
	{
		public PromptScope Scope { get; set; }
		public string ConvKey { get; set; }
		public System.Collections.Generic.IReadOnlyList<string> ParticipantIds { get; set; }
		public int? PawnLoadId { get; set; }
		public bool IsCommand { get; set; }
		public string Locale { get; set; }
		public string UserInput { get; set; }
	}

	internal sealed class ContextBlock
	{
		public string Title { get; set; }
		public string Text { get; set; }
	}

	internal sealed class PromptBuildResult
	{
		public string SystemPrompt { get; set; }
		public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; set; }
		public string UserPrefixedInput { get; set; }
	}

	internal sealed class PromptBuildContext
	{
		public PromptBuildRequest Request { get; set; }
		public string Locale { get; set; }
		public string EntityId { get; set; }
		public RimAI.Core.Source.Modules.World.PawnPromptSnapshot PawnPrompt { get; set; }
		public RimAI.Core.Source.Modules.World.PawnSocialSnapshot PawnSocial { get; set; }
		public RimAI.Core.Source.Modules.World.PawnHealthSnapshot PawnHealth { get; set; }
		public RimAI.Core.Source.Modules.Persona.PersonaRecordSnapshot Persona { get; set; }
		public System.Collections.Generic.IReadOnlyList<RimAI.Core.Source.Modules.History.Models.RecapItem> Recaps { get; set; }
		public RimAI.Core.Source.Modules.World.EnvironmentMatrixSnapshot EnvMatrix { get; set; }
		public System.Func<string, string, string> L { get; set; } // (key,fallback) -> localized
		public System.Func<string, System.Collections.Generic.IDictionary<string,string>, string, string> F { get; set; } // (key,args,fallback) -> formatted localized
	}

	internal sealed class ComposerOutput
	{
		public System.Collections.Generic.IReadOnlyList<string> SystemLines { get; set; }
		public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; set; }
	}
}


