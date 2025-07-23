using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Architecture.Models;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// A placeholder for a future dispatcher that would use local, on-device embedding models
    /// to perform tool selection via semantic similarity search.
    /// This provides an ultra-fast, offline-capable, and cost-free alternative for tool dispatching.
    /// </summary>
    public class EmbeddingDispatcherService : IDispatcherService
    {
        public Task<DispatchResult> DispatchAsync(string userInput, List<AITool> tools, CancellationToken cancellationToken = default)
        {
            // This is a placeholder for future implementation.
            // A real implementation would involve:
            // 1. Loading a local sentence-transformer model (e.g., via ONNX Runtime).
            // 2. Pre-calculating and caching embeddings for all tool descriptions.
            // 3. Calculating the embedding for the user input.
            // 4. Performing a cosine similarity search to find the best matching tool.
            // 5. Extracting parameters, which is a non-trivial challenge in this approach.
            // 6. Returning the result.
            
            Log.Warning("[EmbeddingDispatcherService] This service is not yet implemented and will always return a null result.");

            // Since this is not yet implemented, we return a failed result.
            return Task.FromResult(new DispatchResult { ToolName = null });
        }
    }
} 