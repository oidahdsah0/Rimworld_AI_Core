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
		public PromptScope Scope { get; init; }
		public string ConvKey { get; init; }
		public System.Collections.Generic.IReadOnlyList<string> ParticipantIds { get; init; }
		public int? PawnLoadId { get; init; }
		public bool IsCommand { get; init; }
		public string Locale { get; init; }
		public string UserInput { get; init; }
	}

	internal sealed class ContextBlock
	{
		public string Title { get; init; }
		public string Text { get; init; }
	}

	internal sealed class PromptBuildResult
	{
		public string SystemPrompt { get; init; }
		public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; init; }
		public string UserPrefixedInput { get; init; }
	}

	internal sealed class PromptBuildContext
	{
		public PromptBuildRequest Request { get; init; }
		public string Locale { get; init; }
		public string EntityId { get; init; }
		public RimAI.Core.Source.Modules.World.PawnPromptSnapshot PawnPrompt { get; init; }
		public RimAI.Core.Source.Modules.World.PawnSocialSnapshot PawnSocial { get; init; }
		public RimAI.Core.Source.Modules.Persona.PersonaRecordSnapshot Persona { get; init; }
		public System.Collections.Generic.IReadOnlyList<RimAI.Core.Source.Modules.History.Recap.RecapItem> Recaps { get; init; }
		public System.Func<string, string, string> L { get; init; } // (key,fallback) -> localized
		public System.Func<string, System.Collections.Generic.IDictionary<string,string>, string, string> F { get; init; } // (key,args,fallback) -> formatted localized
	}

	internal sealed class ComposerOutput
	{
		public System.Collections.Generic.IReadOnlyList<string> SystemLines { get; init; }
		public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; init; }
	}
}


