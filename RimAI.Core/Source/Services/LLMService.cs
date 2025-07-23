using RimAI.Core.Architecture.Interfaces;
using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Services
{
    /// <summary>
    /// LLM服务包装器 - 提供对Framework API的统一访问
    /// </summary>
    public class LLMService : ILLMService
    {
        public LLMService() { }

        public bool IsStreamingAvailable => RimAIAPI.IsInitialized;
        public bool IsInitialized => RimAIAPI.IsInitialized;

        public async Task<LLMResponse> SendMessageAsync(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            
            var content = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);

            if (content != null)
            {
                // The Framework API returns a string, but the Core interface expects an LLMResponse.
                // We construct a successful response object here.
                return LLMResponse.Success(content);
            }
            else
            {
                // If the Framework returns null, it indicates an error.
                return LLMResponse.Failed("The request to the RimAI Framework failed or returned no content.");
            }
        }

        public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default) where T : class
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            var jsonContent = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
            if (string.IsNullOrEmpty(jsonContent)) return null;
            // The content of the response is expected to be the JSON for type T
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(jsonContent);
        }

        public Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            return RimAIAPI.SendStreamingMessageAsync(prompt, onChunk, options, cancellationToken);
        }
    }
}
