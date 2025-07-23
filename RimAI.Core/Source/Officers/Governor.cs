using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using RimAI.Core.Officers.Base;
using RimAI.Core.Services;
using RimAI.Framework.LLM.Models;
using Verse;
using Newtonsoft.Json;

namespace RimAI.Core.Officers
{
    public class Governor : OfficerBase
    {
        private readonly IToolRegistryService _toolRegistry;

        public Governor()
        {
            // Resolve services from the central container.
            _toolRegistry = ServiceContainer.Instance.GetService<IToolRegistryService>();

            if (_toolRegistry == null)
            {
                throw new InvalidOperationException("ToolRegistryService is not available.");
            }
        }

        public override string Name => "总督";
        public override string Description => "殖民地的首席AI决策官，负责宏观战略和处理玩家的直接查询。";
        public override OfficerRole Role => OfficerRole.Governor;
        public override string IconPath => "UI/Icons/Governor";

        /// <summary>
        /// Handles a user's query using the new two-stage, AI-driven tool-use workflow.
        /// </summary>
        public async Task<string> HandleUserQueryAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            // STAGE 1: Dispatch - Let the AI decide which tool to use.
            var dispatcher = DispatcherFactory.Create();
            var availableTools = _toolRegistry.GetAvailableTools();
            var dispatchResult = await dispatcher.DispatchAsync(userQuery, availableTools);

            if (dispatchResult == null || !dispatchResult.Success)
            {
                // If the AI decides no tool is needed, or fails to decide,
                // we can fall back to a general conversational response.
                return await FallbackConversationAsync(userQuery, cancellationToken);
            }

            // STAGE 2: Execute - Run the selected tool and generate a response based on its output.
            var executionInfo = _toolRegistry.GetToolExecutionInfo(dispatchResult.ToolName);
            if (executionInfo == null)
            {
                Log.Error($"[Governor] Dispatcher selected tool '{dispatchResult.ToolName}', but it's not registered correctly.");
                return await FallbackConversationAsync(userQuery, cancellationToken);
            }

            // Get the required service instance from the container.
            var serviceInstance = ServiceContainer.Instance.GetService(executionInfo.ServiceType);
            if (serviceInstance == null)
            {
                Log.Error($"[Governor] Tool '{dispatchResult.ToolName}' requires service '{executionInfo.ServiceType.Name}', but it's not in the container.");
                return await FallbackConversationAsync(userQuery, cancellationToken);
            }

            // Execute the tool's logic using the delegate from the registry.
            var toolResult = await executionInfo.Executor(serviceInstance, dispatchResult.Parameters);

            // STAGE 3: Generate - Use the tool's output to generate a final, user-facing response.
            return await GenerateFinalResponseAsync(userQuery, toolResult, cancellationToken);
        }

        /// <summary>
        /// Generates a final, user-facing response based on the original query and the tool's output.
        /// </summary>
        private async Task<string> GenerateFinalResponseAsync(string userQuery, string toolResult, CancellationToken cancellationToken)
        {
            var llmService = ServiceContainer.Instance.GetService<ILLMService>();
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are the Governor, an AI assistant.");
            promptBuilder.AppendLine($"A user asked: \"{userQuery}\".");
            promptBuilder.AppendLine("To answer this, you executed an internal tool which provided the following data:");
            promptBuilder.AppendLine("--- TOOL RESULT ---");
            promptBuilder.AppendLine(toolResult);
            promptBuilder.AppendLine("--------------------");
            promptBuilder.AppendLine("Based on this data, provide a helpful and concise response to the user.");

            var response = await llmService.SendMessageAsync(promptBuilder.ToString(), null, cancellationToken);
            
            // Extract the actual text content from the final response
            return response.IsSuccess ? response.Content : "An unknown error occurred.";
        }

        /// <summary>
        /// A fallback handler for when no specific tool is called.
        /// </summary>
        private async Task<string> FallbackConversationAsync(string userQuery, CancellationToken cancellationToken)
        {
            var llmService = ServiceContainer.Instance.GetService<ILLMService>();
            var response = await llmService.SendMessageAsync($"The user said: \"{userQuery}\". Respond as a helpful AI assistant.", null, cancellationToken);
            
            return response.IsSuccess ? response.Content : "Sorry, I'm having trouble understanding that right now.";
        }
        
        // The old ExecuteAdviceRequest method would be refactored or removed
        // to use this new, more flexible architecture. For now, it is omitted for clarity.
        protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
        {
            // Re-implement the abstract method to delegate to our new, powerful workflow.
            return await HandleUserQueryAsync("Provide a general analysis and give me some advice.", cancellationToken);
        }

        public override async Task<string> GetAdviceAsync(string topic, CancellationToken cancellationToken = default)
        {
            var options = new LLMRequestOptions { Temperature = 0.5f };
            var response = await ServiceContainer.Instance.GetService<ILLMService>().SendMessageAsync(topic, options, cancellationToken);
            
            return response.IsSuccess ? response.Content : "I have no advice on this topic.";
        }
    }
}
