using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure.Persistence;
using Verse;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld GameComponent 负责在存读档生命周期调用 PersistenceService。
    /// 作为 "发令员" 仅做转发，不直接操作 Scribe。
    /// </summary>
    public class PersistenceManager : GameComponent
    {
        private readonly IPersistenceService _persistenceService;
        private readonly IHistoryService _historyService;

        public PersistenceManager(Game game) : base()
        {
            // DI must be ready before any GameComponent is constructed.
            _persistenceService = Infrastructure.CoreServices.Locator.Get<IPersistenceService>();
            _historyService     = Infrastructure.CoreServices.Locator.Get<IHistoryService>();
        }

        public override void ExposeData()
        {
            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                    _persistenceService.PersistHistoryState(_historyService);
                    break;
                case LoadSaveMode.LoadingVars:
                    _persistenceService.LoadHistoryState(_historyService);
                    break;
            }
        }
    }
}