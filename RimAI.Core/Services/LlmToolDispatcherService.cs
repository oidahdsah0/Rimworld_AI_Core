using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Framework.LLM.Models;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// A dispatcher that uses the native 'Tool Calling' feature of an LLM.
    /// This is the preferred, most reliable method for tool selection.
    /// </summary>
    public class LlmToolDispatcherService : IDispatcherService
    {
        private readonly ILLMService _llmService;

        public LlmToolDispatcherService()
        {
            _llmService = ServiceContainer.Instance.GetService<ILLMService>();
            if (_llmService == null)
            {
                // In a real scenario, proper logging or error handling would be here.
                // For now, this makes the dependency clear.
                throw new InvalidOperationException("ILLMService is not available in the ServiceContainer.");
            }
        }

        public async Task<DispatchResult> DispatchAsync(string userInput, List<AITool> tools, CancellationToken cancellationToken = default)
        {
            var options = new LLMRequestOptions().WithTools(tools);
            var response = await _llmService.SendMessageAsync(userInput, options, cancellationToken);

            if (!response.IsSuccess)
            {
                Log.Warning($"[LlmToolDispatcherService] LLM request failed: {response.ErrorMessage}");
                return new DispatchResult { ToolName = null };
            }

            try
            {
                if (response?.ToolCalls == null || !response.ToolCalls.Any())
                {
                    return new DispatchResult { ToolName = null };
                }

                var firstToolCall = response.ToolCalls.First();
                if (firstToolCall?.Function == null || string.IsNullOrEmpty(firstToolCall.Function.Name))
                {
                    Log.Warning("[LlmToolDispatcherService] Received a tool call response without a valid function name.");
                    return new DispatchResult { ToolName = null };
                }

                var parameters = string.IsNullOrEmpty(firstToolCall.Function.Arguments)
                    ? new Dictionary<string, object>()
                    : JsonConvert.DeserializeObject<Dictionary<string, object>>(firstToolCall.Function.Arguments);

                return new DispatchResult
                {
                    ToolName = firstToolCall.Function.Name,
                    Parameters = parameters
                };
            }
            catch (JsonException ex)
            {
                Log.Warning($"[LlmToolDispatcherService] Failed to deserialize tool arguments: {ex.Message}. Raw arguments: {response.ToolCalls?.FirstOrDefault()?.Function?.Arguments}");
                return new DispatchResult { ToolName = null };
            }
        }
    }
} 