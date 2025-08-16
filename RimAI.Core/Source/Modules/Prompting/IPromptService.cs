using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Source.Services.Prompting.Models;

namespace RimAI.Core.Source.Services.Prompting
{
	internal interface IPromptService
	{
		Task<PromptBuildResult> BuildAsync(PromptBuildRequest request, CancellationToken ct = default);
	}
}


