using RimAI.Core.Contracts.Services;
using RimAI.Core.Infrastructure.Persistence;
using RimAI.Core.Services;
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
        private readonly IHistoryWriteService _historyService;
        private readonly IPersonaService _personaService;
        private readonly RimAI.Core.Modules.Persona.IFixedPromptService _fixedPromptService;
        private readonly RimAI.Core.Modules.Persona.IBiographyService _biographyService;
        private readonly RimAI.Core.Modules.History.IRecapService _recapService;
        private readonly RimAI.Core.Modules.Persona.IPersonaBindingService _bindingService;
        private readonly RimAI.Core.Modules.World.IParticipantIdService _participantIdService;

        public PersistenceManager(Game game) : base()
        {
            // DI must be ready before any GameComponent is constructed.
            _persistenceService  = Infrastructure.CoreServices.Locator.Get<IPersistenceService>();
            _historyService      = Infrastructure.CoreServices.Locator.Get<IHistoryWriteService>();
            _personaService      = Infrastructure.CoreServices.Locator.Get<IPersonaService>();
            _fixedPromptService  = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IFixedPromptService>();
            _biographyService    = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IBiographyService>();
            _recapService        = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.History.IRecapService>();
            _bindingService      = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Persona.IPersonaBindingService>();
            _participantIdService= Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.World.IParticipantIdService>();
        }

        public override void ExposeData()
        {
            switch (Scribe.mode)
            {
                case LoadSaveMode.Saving:
                    _persistenceService.PersistHistoryState(_historyService);
                    _persistenceService.PersistPersonaState(_personaService);
                    _persistenceService.PersistFixedPrompts(_fixedPromptService);
                    _persistenceService.PersistBiographies(_biographyService);
                    _persistenceService.PersistRecap(_recapService);
                    _persistenceService.PersistPersonaBindings(_bindingService);
                    _persistenceService.PersistPlayerId(_participantIdService);
                    break;
                case LoadSaveMode.LoadingVars:
                    _persistenceService.LoadHistoryState(_historyService);
                    _persistenceService.LoadPersonaState(_personaService);
                    _persistenceService.LoadFixedPrompts(_fixedPromptService);
                    _persistenceService.LoadBiographies(_biographyService);
                    _persistenceService.LoadRecap(_recapService);
                    _persistenceService.LoadPersonaBindings(_bindingService);
                    _persistenceService.LoadPlayerId(_participantIdService);
                    break;
            }
        }
    }
}