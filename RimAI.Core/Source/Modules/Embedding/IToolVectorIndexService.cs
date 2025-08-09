using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Embedding
{
    internal interface IToolVectorIndexService
    {
        bool IsReady { get; }
        bool IsBuilding { get; }
        string IndexFilePath { get; }

        Task EnsureBuiltAsync();
        void MarkStale();

        Task<IReadOnlyList<ToolMatch>> SearchAsync(string query, IEnumerable<ToolFunction> candidates, int topK, double weightName, double weightDescription);
        Task<ToolMatch> SearchTop1Async(string query, IEnumerable<ToolFunction> candidates, double weightName, double weightDescription);
    }

    internal sealed class ToolMatch
    {
        public string Tool { get; init; }
        public double Score { get; init; }
        public ToolFunction Schema { get; init; }
    }
}


