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

        public Task<string> SendMessageAsync(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            return RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
        }

        public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default) where T : class
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
            if (string.IsNullOrEmpty(response)) return null;
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response);
        }

        public Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMRequestOptions options = null, CancellationToken cancellationToken = default)
        {
            if (!IsInitialized) throw new InvalidOperationException("RimAI Framework is not initialized.");
            return RimAIAPI.SendStreamingMessageAsync(prompt, onChunk, options, cancellationToken);
        }
    }
}
