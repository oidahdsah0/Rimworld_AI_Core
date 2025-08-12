using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Orchestration.PromptOrganizers
{
    internal interface IPromptOrganizer
    {
        string Name { get; }
        Task<RimAI.Core.Modules.Orchestration.PromptAssemblyInput> BuildAsync(PromptContext ctx, CancellationToken ct = default);
    }
}


