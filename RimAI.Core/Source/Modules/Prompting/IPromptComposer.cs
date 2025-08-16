using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting
{
	internal interface IPromptComposer
	{
		PromptScope Scope { get; }
		int Order { get; }
		string Id { get; }
		Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct);
	}
}


