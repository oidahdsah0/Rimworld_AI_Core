using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Models;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// Contains the necessary information to execute a tool, including the C# service type
    /// and a delegate that encapsulates the execution logic.
    /// </summary>
    public class ToolExecutionInfo
    {
        /// <summary>
        /// The C# service type that contains the method to be executed.
        /// </summary>
        public Type ServiceType { get; set; }

        /// <summary>
        /// A delegate that takes the retrieved service instance and AI-provided parameters,
        /// and returns the string result of the tool's execution.
        /// </summary>
        public Func<object, Dictionary<string, object>, Task<string>> Executor { get; set; }
    }

    /// <summary>
    /// Defines the contract for a service that manages the registration and retrieval of AI tools.
    /// It now also provides the direct execution logic for each tool.
    /// </summary>
    public interface IToolRegistryService
    {
        /// <summary>
        /// Gets the complete list of AI-consumable tool definitions.
        /// </summary>
        /// <returns>A list of <see cref="AITool"/> objects ready to be sent to an LLM.</returns>
        List<AITool> GetAvailableTools();

        /// <summary>
        /// Gets the execution information for a given AI tool name.
        /// </summary>
        /// <param name="toolName">The name of the tool as known by the AI.</param>
        /// <returns>A <see cref="ToolExecutionInfo"/> object containing the service type and executor,
        /// or null if no mapping is found.</returns>
        ToolExecutionInfo GetToolExecutionInfo(string toolName);
    }
} 