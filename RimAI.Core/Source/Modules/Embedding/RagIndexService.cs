using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Embedding
{
    /// <summary>
    /// 内存向量索引：最小实现，使用余弦相似度，线程安全集合。
    /// </summary>
    internal sealed class RagIndexService : IRagIndexService
    {
        private readonly ConcurrentDictionary<string, (string Content, float[] Vector)> _docs = new();

        public Task UpsertAsync(string docId, string content, float[] embedding = null)
        {
            if (string.IsNullOrWhiteSpace(docId)) throw new ArgumentException("docId is required");
            content ??= string.Empty;
            _docs[docId] = (content, embedding);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RagHit>> QueryAsync(float[] queryEmbedding, int topK)
        {
            if (queryEmbedding == null || queryEmbedding.Length == 0)
                return Task.FromResult((IReadOnlyList<RagHit>)Array.Empty<RagHit>());

            var scores = new List<RagHit>(_docs.Count);
            foreach (var kv in _docs)
            {
                var docId = kv.Key;
                var content = kv.Value.Content;
                var v = kv.Value.Vector;
                if (v == null || v.Length == 0) continue;
                var score = Cosine(queryEmbedding, v);
                scores.Add(new RagHit { DocId = docId, Content = content, Score = score });
            }

            var result = scores
                .OrderByDescending(h => h.Score)
                .Take(Math.Max(1, topK))
                .ToList();
            return Task.FromResult((IReadOnlyList<RagHit>)result);
        }

        private static float Cosine(float[] a, float[] b)
        {
            var len = Math.Min(a.Length, b.Length);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na <= 1e-8 || nb <= 1e-8) return 0f;
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb)));
        }
    }
}


