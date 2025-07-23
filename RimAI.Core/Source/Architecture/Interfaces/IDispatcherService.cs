using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Models;
using System.Threading;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// Represents the result of a dispatch operation, indicating which tool was selected
    /// and with what parameters.
    /// </summary>
    public class DispatchResult
    {
        /// <summary>
        /// The name of the tool selected by the dispatcher.
        /// </summary>
        public string ToolName { get; set; }

        /// <summary>
        /// The parameters extracted by the dispatcher for the selected tool.
        /// The key is the parameter name, and the value is the parameter value.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
        
        /// <summary>
        /// A flag indicating whether the dispatch operation was successful.
        /// </summary>
        public bool Success => !string.IsNullOrEmpty(ToolName);
    }

    /// <summary>
    /// Defines the contract for a dispatcher service, which is responsible for selecting
    /// the appropriate tool based on user input and a list of available tools.
    /// This service acts as the "AI brain" for deciding "What to do?".
    /// </summary>
    public interface IDispatcherService
    {
        /// <summary>
        /// Asynchronously selects a tool based on user input.
        /// </summary>
        /// <param name="userInput">The natural language input from the user.</param>
        /// <param name="tools">A list of available AI tools to choose from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the
        /// <see cref="DispatchResult"/> with the name of the selected tool and its parameters.</returns>
        Task<DispatchResult> DispatchAsync(string userInput, List<AITool> tools, CancellationToken cancellationToken = default);
    }
} 