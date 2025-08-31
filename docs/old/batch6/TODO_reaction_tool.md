# Cursor ToDos — Pawn Conversation Reaction Tool

- Contracts
  - [ ] Define DTOs: `PawnReactionRequest { int mood_delta; string mood_title; string locale; string pawnId; string playerTitle; string lastUserText; string lastAssistantText; DateTime ts }`
  - [ ] Define response: `PawnReactionResponse { string status; int applied_delta; string applied_title; string note }`
  - [ ] Tool metadata interface + constants: `ToolNames.PawnConversationReaction`

- Tool Implementation (Core)
  - [ ] Handler: `PawnConversationReactionTool` implements `IRimAITool`
  - [ ] Server-side validation & clamping
    - [ ] Clamp delta to [-30, 30]
    - [ ] Title trimming: CJK ≤ 7 chars; non-CJK ≤ 3 words; strip decorative punctuations; fallback to localized default
  - [ ] Persist reaction event (History/Thought)
  - [ ] Register tool in `ToolRegistryService` with localized display name/description

- Orchestration
  - [ ] `ReactionOrchestrationService` background queue
  - [ ] Subscribe to Chat stream-completed event (from GroupChatAct/ChatWindow controller)
  - [ ] Build prompt (localized) with examples; enforce tool call only (non-stream)
  - [ ] Retry once if tool call not emitted

- Localization (Locales/*.json)
  - [ ] Add keys per language:
    - `tool.pawn_reaction.display_name`
    - `tool.pawn_reaction.description` (detailed usage, limits, examples)
    - `tool.pawn_reaction.default_title`
    - `prompt.reaction.system` (system prompt template per-language, only constrain its own language)
    - `prompt.reaction.examples` (full examples list per-language)

- Wiring
  - [ ] Inject ReactionOrchestrationService in DI
  - [ ] Fire event after streaming finished; enqueue background task

- QA
  - [ ] Unit tests for validation rules (delta clamp; title truncation by locale)
  - [ ] Smoke test end-to-end path (mock LLM returns tool call)
  - [ ] Telemetry: log origin msg id, clamp/trunc info, apply result

- Optional
  - [ ] Make reaction feature toggleable in settings
  - [ ] Backoff for repeated failures
