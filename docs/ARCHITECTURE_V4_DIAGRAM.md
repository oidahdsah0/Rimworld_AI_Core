graph TB

classDef layer fill:#f9f9f9,stroke:#333,stroke-width:2px;
classDef module fill:#e6ffe6,stroke:#006600,stroke-width:1px;
classDef infra fill:#e6f3ff,stroke:#0066cc,stroke-width:1px;
classDef external fill:#fff0e6,stroke:#ff6600,stroke-width:1px;

subgraph "UI Layer"
    DebugPanel["Debug Panel"]
    GameWindows["Assistant / Dialog Windows"]
    PersonalityWindow["Personality Window\\n(个性窗体)"]
end
class DebugPanel,GameWindows,PersonalityWindow layer

subgraph "Modules Layer"
    LLM["Module: LLM"]
    WorldAccess["Module: WorldAccess"]
    Tooling["Module: Tooling"]
    Orchestration["Module: Orchestration\\n(Strategies: Classic / EmbeddingFirst)"]
    Persistence["Module: Persistence"]
    Eventing["Module: Eventing"]
    Persona["Module: Persona\\n(Persona CRUD / Binding)"]
    PromptAsm["Module: PromptAssembly\\n(Composer + Templates)"]
    Personalization["Module: Personalization\\n(FixedPrompts / Biography / Beliefs)"]
end
class LLM,WorldAccess,Tooling,Orchestration,Persistence,Eventing,Persona,PromptAsm,Personalization module

subgraph "Infrastructure Layer"
    ServiceContainer["DI Container"]
    Scheduler["SchedulerService"]
    %% Cache moved to Framework layer in v4: Core no longer provides CacheService
    Config["ConfigurationService"]
end
class ServiceContainer,Scheduler,Config infra

subgraph "Contracts (Stable)"
    Contracts["Interfaces & DTOs"]
end

subgraph "External"
    RimWorld["RimWorld Engine"]
    Framework["RimAI.Framework v4.3 API"]
end
class RimWorld,Framework external

%% Relations
GameWindows --> Orchestration
PersonalityWindow --> Personalization
DebugPanel --> LLM
DebugPanel --> Tooling
Orchestration --> LLM
Orchestration --> Tooling
Orchestration --> PromptAsm
PromptAsm --> Personalization
PromptAsm --> Persistence
LLM --> Framework
WorldAccess --> RimWorld
Tooling --> WorldAccess
Scheduler --> RimWorld
Persistence --> RimWorld
%% LLM caching is handled by RimAI.Framework; Core's LLM talks only to Framework
Modules Layer --> ServiceContainer
Infrastructure Layer --> Contracts
Modules Layer --> Contracts
