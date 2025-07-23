using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimAI.Core.Analysis;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;

namespace RimAI.Core.Services
{
    /// <summary>
    /// A private helper class to hold the full registration details for a tool.
    /// </summary>
    internal class RegisteredTool
    {
        public AITool ToolDefinition { get; }
        public ToolExecutionInfo ExecutionInfo { get; }

        public RegisteredTool(AITool toolDefinition, ToolExecutionInfo executionInfo)
        {
            ToolDefinition = toolDefinition;
            ExecutionInfo = executionInfo;
        }
    }

    /// <summary>
    /// Implements the IToolRegistryService with an enhanced design.
    /// It now maps tool names not just to a Type, but to a full ToolExecutionInfo object,
    /// which includes an "Executor" delegate. This encapsulates the exact logic for calling
    /// the tool, including parameter handling, making the Governor's job much simpler.
    /// </summary>
    public class ToolRegistryService : IToolRegistryService
    {
        private readonly List<RegisteredTool> _registeredTools = new List<RegisteredTool>();
        private readonly Dictionary<string, ToolExecutionInfo> _toolExecutionMap = new Dictionary<string, ToolExecutionInfo>();

        public ToolRegistryService()
        {
            RegisterTools();
        }

        private void RegisterTools()
        {
            // Example Tool 1: Get Colony Summary
            Register(
                new AITool
                {
                    Function = new AIFunction { /* ... as before ... */ }
                },
                typeof(IColonyAnalyzer),
                async (service, parameters) =>
                {
                    var analyzer = (IColonyAnalyzer)service;
                    // In a real implementation, this would call a method on the analyzer
                    // return await analyzer.GetColonySummaryAsync();
                    return await Task.FromResult("Colony is thriving, but moods are a bit low due to a lack of recreation.");
                }
            );

            // Example Tool 2: Get Pawn Details
            Register(
                new AITool
                {
                    Function = new AIFunction { /* ... as before ... */ }
                },
                typeof(IPawnAnalyzer),
                async (service, parameters) =>
                {
                    var analyzer = (IPawnAnalyzer)service;
                    if (parameters.TryGetValue("pawnName", out var pawnNameObj) && pawnNameObj is string pawnName)
                    {
                        return await analyzer.GetPawnDetailsAsync(pawnName);
                    }
                    return await Task.FromResult("Error: 'pawnName' parameter was missing or not a string.");
                }
            );
        }

        /// <summary>
        /// Helper to register a tool with its execution logic.
        /// </summary>
        private void Register(AITool toolDefinition, Type serviceType, Func<object, Dictionary<string, object>, Task<string>> executor)
        {
            var executionInfo = new ToolExecutionInfo { ServiceType = serviceType, Executor = executor };
            var tool = new RegisteredTool(toolDefinition, executionInfo);
            
            _registeredTools.Add(tool);
            _toolExecutionMap[toolDefinition.Function.Name] = executionInfo;
        }

        public List<AITool> GetAvailableTools()
        {
            return _registeredTools.Select(t => t.ToolDefinition).ToList();
        }

        public ToolExecutionInfo GetToolExecutionInfo(string toolName)
        {
            return _toolExecutionMap.TryGetValue(toolName, out var info) ? info : null;
        }
    }
} 