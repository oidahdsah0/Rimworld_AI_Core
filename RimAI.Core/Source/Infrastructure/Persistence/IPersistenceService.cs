using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Models;

namespace RimAI.Core.Infrastructure.Persistence
{
    /// <summary>
    /// 持久化服务 —— "总工程师"。
    /// 仅在此文件中与 Verse.Scribe 交互，其他服务不得直接引用 Scribe API。
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// 将 <see cref="IHistoryService"/> 当前状态写入存档。
        /// </summary>
        void PersistHistoryState(IHistoryService historyService);

        /// <summary>
        /// 从存档加载状态并写入 <see cref="IHistoryService"/>。
        /// </summary>
        void LoadHistoryState(IHistoryService historyService);

        /// <summary>
        /// 将 <see cref="IPersonaService"/> 当前状态写入存档。
        /// </summary>
        void PersistPersonaState(IPersonaService personaService);

        /// <summary>
        /// 从存档加载状态并写入 <see cref="IPersonaService"/>。
        /// </summary>
        void LoadPersonaState(IPersonaService personaService);
    }
}