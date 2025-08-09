using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 历史查询只读接口。对第三方仅暴露读取能力。
    /// </summary>
    public interface IHistoryQueryService
    {
        /// <summary>
        /// 按参与者组合检索主线 + 背景历史。
        /// </summary>
        /// <param name="participantIds">参与者稳定 ID 列表。</param>
        Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds);
    }
}


