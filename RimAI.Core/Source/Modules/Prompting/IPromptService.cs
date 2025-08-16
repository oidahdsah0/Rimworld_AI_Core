using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Modules.Prompting.Models;

namespace RimAI.Core.Source.Modules.Prompting
{
	internal interface IPromptService
	{
		Task<PromptBuildResult> BuildAsync(PromptBuildRequest request, CancellationToken ct = default);
	}
}


