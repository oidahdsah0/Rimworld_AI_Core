using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.LLM
{
    public interface ILLMService
    {
        Task<string> GetResponseAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default);
    }
}
