using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Services.Prompting.Models;

namespace RimAI.Core.Source.Services.Prompting
{
	internal interface IPromptComposer
	{
		PromptScope Scope { get; }
		int Order { get; }
		string Id { get; }
		Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, CancellationToken ct);
	}
}


