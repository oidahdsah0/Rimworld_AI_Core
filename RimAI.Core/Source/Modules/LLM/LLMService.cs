using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.API;
using RimAI.Framework.Contracts;
using RimAI.Core.Infrastructure.Configuration;

namespace RimAI.Core.Modules.LLM
{
    /// <summary>
    /// Minimal LLM gateway wrapping RimAIApi (P2). Caching / Retry policies will be added later.
    /// </summary>
    public class LLMService : ILLMService
    {
        private readonly IConfigurationService _config;

        public LLMService(IConfigurationService config)
        {
            _config = config;
        }

        public async Task<string> GetResponseAsync(UnifiedChatRequest request, CancellationToken cancellationToken = default)
        {
            // Apply global temperature from config if caller didn't set

            var result = await RimAIApi.GetCompletionAsync(request!, cancellationToken);
            if (result.IsSuccess)
            {
                return result.Value!.Message!.Content ?? string.Empty;
            }
            else
            {
                // For now throw; later convert to LLMException etc.
                throw new System.Exception($"LLM request failed: {result.Error}");
            }
        }
    }
}
