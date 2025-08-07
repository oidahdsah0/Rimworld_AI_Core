graph TB
    
    classDef layer fill:#f9f9f9,stroke:#333,stroke-width:2px;
    classDef service fill:#e6f3ff,stroke:#0066cc,stroke-width:1px;
    classDef brain fill:#e6ffe6,stroke:#006600,stroke-width:2px;
    classDef external fill:#fff0e6,stroke:#ff6600,stroke-width:1px;
    classDef io fill:#f2e6ff,stroke:#6600cc,stroke-width:1px;

    subgraph "Layer 1: Presentation (UI & Agents)"
        UserUI("User Interaction<br/>(MainTab, Dialogs)")
        PersonaService("IPersonaService")
    end
    class UserUI,PersonaService layer

    subgraph "Layer 2: Application (The Brain)"
        OrchestrationService("IOrchestrationService")
    end
    class OrchestrationService brain

    subgraph "Layer 3: Domain Services (Core Logic)"
        PromptFactoryService("IPromptFactoryService")
        HistoryService("IHistoryService")
        ToolRegistryService("IToolRegistryService")
        EventAggregator("IEventAggregatorService")
    end
    class PromptFactoryService,HistoryService,ToolRegistryService,EventAggregator service

    subgraph "Layer 4: Infrastructure & Data (The Plumbing)"
        subgraph "World I/O (Anti-Corruption)"
            WorldDataService("IWorldDataService<br/>(Safe READ)")
            CommandService("ICommandService<br/>(Safe WRITE)")
        end
        subgraph "LLM & Persistence"
            LLMService("ILLMService")
            PersistenceService("IPersistenceService")
        end
        subgraph "Low-Level Utilities"
            SchedulerService("ISchedulerService")
            CacheService("ICacheService")
            ConfigService("IConfigurationService")
            EventBus("IEventBus")
        end
    end
    class WorldDataService,CommandService,LLMService,PersistenceService,SchedulerService,CacheService,ConfigService,EventBus io

    subgraph "External Systems"
        GameEngine("RimWorld Game State<br/>(Defs, Data, Events)")
        FrameworkAPI("RimAI.Framework API")
    end
    class GameEngine,FrameworkAPI external

    %% --- Main Flow (Tool-Assisted Query) ---
    UserUI -- "1. User Query" --> OrchestrationService
    OrchestrationService -- "2. Build Prompt" --> PromptFactoryService
    PromptFactoryService -- "3. Gather Context" --> HistoryService
    PromptFactoryService -- "3. Gather Context" --> WorldDataService
    PromptFactoryService -- "3. Gather Context" --> ToolRegistryService
    OrchestrationService -- "4. Call LLM (for tool use)" --> LLMService
    LLMService -- "5. Get Tool Call" --> OrchestrationService
    OrchestrationService -- "6. Execute Tool" --> ToolRegistryService
    ToolRegistryService -- "calls tool which uses" --> CommandService
    ToolRegistryService -- "calls tool which uses" --> WorldDataService
    OrchestrationService -- "7. Record History" --> HistoryService
    OrchestrationService -- "8. Stream Final Response" --> UserUI
    
    %% --- Other Key Connections ---
    LLMService --> FrameworkAPI
    LLMService -- "uses" --> CacheService
    LLMService -- "uses" --> ConfigService
    WorldDataService -- "uses" --> SchedulerService
    CommandService -- "uses" --> SchedulerService
    SchedulerService -- "schedules on" --> GameEngine
    UserUI -- "gets persona" --> PersonaService
    PersonaService -- "loads templates from" --> GameEngine
    PersistenceService -- "on save/load" --> GameEngine
    PersistenceService -- "manages state for" --> HistoryService
    EventAggregator -- "subscribes to" --> EventBus
    EventBus -- "receives from" --> GameEngine
    EventAggregator -- "calls" --> LLMService

    %% All services are registered in a ServiceContainer.
    %% All services can depend on IConfigurationService. 