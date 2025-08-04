# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased] - 2025-07-27

### Added
- Framework v4.1 compatibility layer (`ILLMService` new `SendChatAsync`, `StreamResponseAsync`, `SendChatWithToolsAsync`)
- `CacheKeyUtil` (SHA256 key generation for `UnifiedChatRequest`)
- `IOrchestrationService` 流式接口 & `ContinueWithToolResultsAsync`
- MVP 实现：`OrchestrationService`, `PromptFactoryService`, `ToolRegistryService`, `HistoryService`
- Core contracts: `IRimAITool`, `IToolRegistryService`, `IPromptFactoryService`, `IHistoryService`

### Changed
- `ILLMService` 旧接口标记 `[Obsolete]`; 计划 v3.2 移除
- CacheService 泛型值改为 `UnifiedChatResponse`

### Deprecated
- `SendMessageAsync`, `StreamMessageAsync`, `GetToolCallsAsync` (将在 v3.2 移除)

### Removed
- N/A

### Fixed
- N/A

### Security
- Centralized tool execution path to prevent arbitrary game-state writes

## [1.0.0] - TBD

### Added
- Three-Officer System (Governor, Military Officer, Logistics Officer)
- Direct Command Interface with natural language processing
- W.I.F.E. System for workflow optimization
- Dual-layer architecture (LLM Layer + Execution Layer)
- Material Depth System (5 levels of analysis)
- Action System (Query, Suggest, Execute, Monitor)
- Multi-language support
- Integration with RimAI Framework
