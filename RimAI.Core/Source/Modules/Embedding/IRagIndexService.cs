using System.Collections.Generic;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Embedding
{
    internal interface IRagIndexService
    {
        Task UpsertAsync(string docId, string content, float[] embedding = null);
        Task<IReadOnlyList<RagHit>> QueryAsync(float[] queryEmbedding, int topK);
    }

    internal sealed class RagHit
    {
        public string DocId { get; init; }
        public string Content { get; init; }
        public float Score { get; init; }
    }
}


