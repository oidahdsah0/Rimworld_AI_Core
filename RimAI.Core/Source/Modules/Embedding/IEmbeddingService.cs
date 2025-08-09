using System.Threading.Tasks;

namespace RimAI.Core.Modules.Embedding
{
    /// <summary>
    /// 文本向量化服务。S2 最小实现：本地确定性向量（后续可切换为 Framework Embedding API）。
    /// </summary>
    internal interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
        Task<bool> IsAvailableAsync();
    }
}


