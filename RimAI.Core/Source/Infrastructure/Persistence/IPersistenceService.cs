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
        /// 固定提示词（主存：pawnId -> text；覆盖层如需持久化可加新节点）。
        /// </summary>
        void PersistFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService);

        /// <summary>
        /// 加载固定提示词主存快照。
        /// </summary>
        void LoadFixedPrompts(RimAI.Core.Modules.Persona.IFixedPromptService fixedPromptService);

        /// <summary>
        /// 人物传记（后续切换为 pawnId -> List）。
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

        /// <summary>
        /// 人格绑定（pawnId -> personaName#rev）。
        /// </summary>
        void PersistPersonaBindings(RimAI.Core.Modules.Persona.IPersonaBindingService bindingService);

        /// <summary>
        /// 加载人格绑定。
        /// </summary>
        void LoadPersonaBindings(RimAI.Core.Modules.Persona.IPersonaBindingService bindingService);

        /// <summary>
        /// 玩家稳定 ID（player:&lt;saveInstanceId&gt;）。
        /// </summary>
        void PersistPlayerId(RimAI.Core.Modules.World.IParticipantIdService participantIdService);

        /// <summary>
        /// 读取玩家稳定 ID。
        /// </summary>
        void LoadPlayerId(RimAI.Core.Modules.World.IParticipantIdService participantIdService);
    }
}