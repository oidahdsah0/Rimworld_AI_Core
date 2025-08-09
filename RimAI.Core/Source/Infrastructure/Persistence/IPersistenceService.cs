using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Services;

namespace RimAI.Core.Infrastructure.Persistence
{
    /// <summary>
    /// 持久化服务 —— "总工程师"。
    /// 仅在此文件中与 Verse.Scribe 交互，其他服务不得直接引用 Scribe API。
    /// </summary>
    internal interface IPersistenceService
    {
        /// <summary>
        /// 将 <see cref="IHistoryWriteService"/> 当前状态写入存档。
        /// </summary>
        void PersistHistoryState(IHistoryWriteService historyService);

        /// <summary>
        /// 从存档加载状态并写入 <see cref="IHistoryWriteService"/>。
        /// </summary>
        void LoadHistoryState(IHistoryWriteService historyService);

        /// <summary>
        /// 将 <see cref="IPersonaService"/> 当前状态写入存档。
        /// </summary>
        void PersistPersonaState(IPersonaService personaService);

        /// <summary>
        /// 从存档加载状态并写入 <see cref="IPersonaService"/>。
        /// </summary>
        void LoadPersonaState(IPersonaService personaService);

        /// <summary>
        /// 按会话键持久化固定提示词（convKey => (participantId => text)）。
        /// </summary>
        void PersistFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService);

        /// <summary>
        /// 加载固定提示词快照。
        /// </summary>
        void LoadFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService);

        /// <summary>
        /// 按会话键持久化人物传记（仅 1v1）。
        /// </summary>
        void PersistBiographies(RimAI.Core.Modules.Persona.IBiographyService biographyService);

        /// <summary>
        /// 加载人物传记快照。
        /// </summary>
        void LoadBiographies(RimAI.Core.Modules.Persona.IBiographyService biographyService);

        /// <summary>
        /// 按会话键持久化前情提要（Recap 字典）。
        /// </summary>
        void PersistRecap(RimAI.Core.Modules.History.IRecapService recapService);

        /// <summary>
        /// 加载前情提要快照。
        /// </summary>
        void LoadRecap(RimAI.Core.Modules.History.IRecapService recapService);
    }
}