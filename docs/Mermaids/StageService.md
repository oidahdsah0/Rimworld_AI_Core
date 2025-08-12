```mermaid
flowchart TD

  %% =====================
  %% Stage (Thin, P11.5)
  %% =====================
  subgraph "Stage Service (Thin, P11.5)"
    ST_API["IStageService\n- Register/Unregister/Enable/Disable Acts & Triggers\n- SubmitIntent(intent)\n- StartAsync(req) (Debug/Script)\n- QueryRunning()"]
    ST_REQ["StageExecutionRequest{<br/>actName, participants[], convKey?, userInputOrScenario?, seed?, locale?, targetParticipantId?<br/>}"]
    ST_INTENT["StageIntent{ actName, participants[], convKey, origin, scenario?, priority?, seed?, locale? }"]
    ST_DECISION["StageDecision{ Approve|Reject|Defer, reason?, ticket? }"]
  end

  subgraph "Stage Kernel (Arbitration)"
    KERN["IStageKernel\n- TryReserve/ExtendLease/Release\n- CoalesceWithin(convKey, windowMs)\n- Cooldown(IsIn/Set)\n- Idempotency(Get/Set)"]
  end

  subgraph "Eligibility & Keys"
    ELIG["Eligibility 校验\nMinParticipants/MaxParticipants/PermittedOrigins/Rules"]
    KEYGEN["convKey = sort(participants).join('|')\nseed = hash(convKey) or request.seed"]
  end

  subgraph "Triggers"
    TR_IF["IStageTrigger\n- Subscribe/OnEnable/OnDisable\n- RunOnceAsync() (主动扫描)"]
    TR_GC["GroupChatTrigger\nProximityScan: RangeK + Probability|Threshold"]
  end

  subgraph "Acts"
    ACT_IF["IStageAct\nIsEligible(ctx) / RunAsync(ctx) → ActResult{ FinalText, Rounds, Reason }"]
    ACT_GC["GroupChatAct\n- 选题与开场白(Topic)\n- 稳定随机顺序(seed)\n- 多轮 Persona 非流式"]
    TOPIC["TopicSelector (权重/去重/裁剪)\n→ TopicSelected 事件\n→ IFixedPromptService.UpsertConvKeyOverride"]
  end

  subgraph "Persona Pipeline"
    PER_CONV["IPersonaConversationService"]
    PROMPT["IPromptAssemblyService"]
    PERSO["Personalization\n(FixedPrompts/Biography/Beliefs)"]
  end

  subgraph "Sinks & Events"
    EVTS["ActStarted / ActTurnCompleted / ActFinished / ActRejected / ActPreempted\nTopicSelected"]
    SINK["StageHistorySink\n仅写‘最终输出’"]
  end

  subgraph "Config"
    CFG_STAGE["CoreConfig.Stage{\nCoalesceWindowMs, CooldownSeconds, Min/MaxParticipants,\nDisabledActs, DisabledTriggers, MaxLatencyMsPerTurn, LocaleOverride,\nRetryPolicy, PermittedOrigins, EligibilityRules,\nScan{Enabled, IntervalSeconds, MaxNewConversationsPerScan},\nProximityScan{Enabled, RangeK, TriggerMode, TriggerThreshold, ProbabilityP, OnlyNonHostile, ExcludeBusy},\nTopic{Enabled, Sources(weights), MaxChars, DedupWindow, SeedPolicy, Locale}\n}"]
    CFG_GC["GroupChatConfig{\nRounds, ProximityScan(...), Topic{Sources, MaxChars, DedupWindow, SeedPolicy}\n}"]
  end

  %% ===== Flows =====
  %% Triggered path
  TR_IF -->|StageIntent| ST_API
  TR_GC --> TR_IF
  ST_API --> ELIG --> KEYGEN --> KERN
  KERN -->|StageDecision| ST_DECISION
  ST_DECISION -- "Approve(ticket)" --> ACT_IF
  ACT_IF -->|Execute(GroupChat)| ACT_GC
  ACT_GC --> TOPIC --> EVTS
  ACT_GC --> PER_CONV --> PROMPT --> PERSO
  ACT_GC -->|Emit| EVTS --> SINK
  ST_API -->|StartAsync(req) (Debug)| ST_REQ --> ACT_IF

  %% Config wiring
  CFG_STAGE --> KERN
  CFG_STAGE --> ELIG
  CFG_STAGE --> TR_IF
  CFG_GC --> TR_GC
  CFG_GC --> ACT_GC
```