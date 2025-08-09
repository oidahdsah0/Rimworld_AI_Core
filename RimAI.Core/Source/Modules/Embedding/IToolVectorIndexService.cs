using System.Threading.Tasks;

namespace RimAI.Core.Modules.Embedding
{
    internal interface IToolVectorIndexService
    {
        bool IsReady { get; }
        bool IsBuilding { get; }
        string IndexFilePath { get; }

        Task EnsureBuiltAsync();
    }
}


