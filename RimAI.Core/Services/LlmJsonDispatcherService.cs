using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Framework.LLM.Models;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// A dispatcher that uses the 'JSON Mode' of an LLM to force it to select a tool.
    /// This is a fallback method and is generally less reliable than native tool calling.
    /// </summary>
    public class LlmJsonDispatcherService : IDispatcherService
    {
        private readonly ILLMService _llmService;

        public LlmJsonDispatcherService()
        {
            _llmService = ServiceContainer.Instance.GetService<ILLMService>();
            if (_llmService == null)
            {
                throw new InvalidOperationException("ILLMService is not available in the ServiceContainer.");
            }
        }

        public async Task<DispatchResult> DispatchAsync(string userInput, List<AITool> tools, CancellationToken cancellationToken = default)
        {
            // Construct a detailed system prompt to guide the LLM's behavior.
            var systemPrompt = BuildSystemPrompt(tools);
            
            var options = new LLMRequestOptions()
                .WithJsonOutput(true) // Force JSON mode
                .WithSystemPrompt(systemPrompt); // Provide our detailed instructions

            var response = await _llmService.SendMessageAsync(userInput, options, cancellationToken);

            if (!response.IsSuccess || string.IsNullOrEmpty(response.Content))
            {
                Log.Warning($"[LlmJsonDispatcherService] LLM request failed or returned empty content. Error: {response.ErrorMessage}");
                return new DispatchResult { ToolName = null };
            }

            try
            {
                // The LLM's response content should be the JSON object we requested.
                var dispatchResult = JsonConvert.DeserializeObject<DispatchResult>(response.Content);
                return dispatchResult ?? new DispatchResult { ToolName = null };
            }
            catch (JsonException ex)
            {
                Log.Warning($"[LlmJsonDispatcherService] Failed to deserialize JSON response from LLM: {ex.Message}. Raw content: {response.Content}");
                return new DispatchResult { ToolName = null };
            }
        }

        /// <summary>
        /// Builds a system prompt that instructs the LLM to act as a tool dispatcher
        /// and return a specific JSON format.
        /// </summary>
        private string BuildSystemPrompt(List<AITool> tools)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a highly intelligent function-calling AI model.");
            sb.AppendLine("Your task is to analyze the user's request and determine which of the following tools, if any, should be called.");
            sb.AppendLine("The available tools are:");
            sb.AppendLine("```json");
            sb.AppendLine(JsonConvert.SerializeObject(tools, Formatting.Indented));
            sb.AppendLine("```");
            sb.AppendLine("You MUST respond with a single JSON object matching the following schema:");
            sb.AppendLine("```json");
            sb.AppendLine("{ \"type\": \"object\", \"properties\": { \"tool_name\": { \"type\": \"string\", \"description\": \"Name of the selected tool.\" }, \"parameters\": { \"type\": \"object\", \"description\": \"Parameters for the tool.\" } } }");
            sb.AppendLine("```");
            sb.AppendLine("If no tool is appropriate, you must respond with: { \"tool_name\": null, \"parameters\": null }");
            sb.AppendLine("Do not include any other text, explanations, or markdown formatting in your response. Only the JSON object is allowed.");

            return sb.ToString();
        }
    }
} 