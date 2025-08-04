using System.Threading.Tasks;
using Verse;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 游戏世界数据安全读取服务（防腐层）。所有对 Verse API 的读取必须通过此接口。
    /// </summary>
    public interface IWorldDataService
    {
        /// <summary>
        /// 获取当前游戏总刻数。示例方法，用于演示线程安全调用。
        /// </summary>
        Task<int> GetCurrentGameTickAsync();

        // 未来：Task<ColonySummary> GetColonySummaryAsync();
        // 未来：Task<PawnSummary> GetPawnSummaryAsync(Pawn pawn);
    }
}