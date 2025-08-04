以下清单覆盖当前项目（Core + Framework）所有应重点检查的“模块 / 目录 / 组件”。已按层次与职责分组，方便逐一核对。

一、RimAI Core（v3 架构）
1. Contracts（所有接口与数据契约）
   • Services：ILLMService、IOrchestrationService、ISchedulerService、ICacheService、IConfigurationService、IWorldDataService、ICommandService、IHistoryService、IToolRegistryService、IEventBus、IEventAggregatorService（🚧）、IPersistenceService、IPromptFactoryService、IPersonaService（🚧）  
   • Data：CoreConfig、Conversation／HistoryStateSnapshot、StreamingModels 等  
   • Events：IEvent 及具体事件类（TickEvent 等）  
   • Tools：IRimAITool  
2. Architecture（技术基础设施）
   • DI：ServiceContainer、CoreServices  
   • Scheduling：SchedulerService  
   • Caching：CacheService  
3. Services（核心业务 & 基础能力）
   • LLMService  
   • OrchestrationService  
   • ConfigurationService  
   • HistoryService  
   • PromptFactoryService  
   • WorldDataService  
   • CommandService  
   • ToolRegistryService  
   • EventBus、EventAggregatorService（🚧 智能聚合）  
   • PersistenceService  
   • PersonaService（🚧 个性化）  
4. Lifecycle
   • RimAIMod（Mod 入口，注册容器）  
   • PersistenceManager（存读档钩子）  
5. Tools（具体 AI 工具实现）
   • GetColonyStatusTool … 及后续新增工具  
6. UI
   • MainTabWindow_RimAI  
   • RimAISettings（设置界面）  
   • *未来* PawnDialogWindow、SettingsWindow 等  
7. Events / Hooks
   • GameEventHooks  
   • TickEvent  
8. Exceptions（自定义异常）
   • LLMException、ConfigurationException、ToolExecutionException、FrameworkException、RimAIException …  

二、RimAI Framework（v4.3 API）
1. API
   • RimAIApi（静态统一入口，含 StreamCompletionAsync / GetCompletionAsync 等）  
2. Configuration
   • BuiltInTemplates  
   • ChatModels、EmbeddingModels  
   • SettingsManager  
3. Core
   • ChatManager、EmbeddingManager  
   • Lifecycle／FrameworkDI  
4. Execution
   • HttpClientFactory、HttpExecutor、RetryPolicy  
5. Translation
   • ChatRequestTranslator、ChatResponseTranslator  
   • EmbeddingRequestTranslator、EmbeddingResponseTranslator  
6. Shared
   • Exceptions：ConfigurationException、FrameworkException、LLMException  
   • Logging：RimAILogger  
   • Models（通用 DTO）  
7. UI
   • RimAIFrameworkMod、RimAIFrameworkSettings  
8. Contracts（独立程序集 RimAI.Framework.Contracts）
   • Models：Result、ToolingModels、UnifiedChatModels、UnifiedEmbeddingModels  

检查建议：  
• 先确认 Contracts 与 Architecture 层接口/基础设施是否完善；  
• 再逐项对 Services 与 Framework API 的对接、实现状态、测试覆盖进行检查；  
• 对照 V3_IMPLEMENTATION_PLAN 的 “🚧” 条目，重点关注 EventAggregatorService、PersonaService 及异常/韧性补全。

---
### 📈 检查进度日志（2025-08-05 更新）

#### ✅ 已完成
1. **DI 基石**
   • `ServiceContainer` 全自动构造函数注入  
   • `CoreServices` 静态门面
2. **LLM 防火墙 & 韦性**  
   • `ILLMService` v4.3 对接完成：`SendChatAsync` / `StreamResponseAsync` / `SendChatWithToolsAsync`  
   • 实现统一 **RetryPolicy (指数退避 3 次)**、**CircuitBreaker (窗口=60s，阈值=5，冷却=300s)**  
   • 流式结束后写入缓存，非流式请求命中缓存立即返回  
   • 日志埋点：缓存命中 / 重试次数 / 熔断开启 / 耗时  
   • `CoreConfig` 扩展 `LLM.Resilience` & `Cache` 节点  
   • `CacheKeyUtil` 标准化 JSON + 模型名 → SHA256，解决键不稳定问题
3. **缓存子系统**  
   • `CacheService` 线程安全 `ConcurrentDictionary` + 过期清理  
   • 命中 / 过期 / 未命中 日志输出
4. **可空性**  
   • 项目级 `#nullable` 已在关键文件启用并清理警告
5. **文档同步**  
   • `CoreConfig` / 缓存策略 / 韦性策略写入 V3 文档

#### 🚧 进行中
- **Unit Test 套件**：编写针对 `ILLMService` 的缓存 / 重试 / 熔断 / 流式写缓存四大场景 
- **OrchestrationService**：接入新 ILLMService 流式 API、完善异常反馈逻辑

#### 📝 待办关键模块（按优先级）
1. EventAggregatorService（智能事件聚合）
2. PersonaService ＆ Persona 数据模型
3. HistoryService 性能与持久化完善
4. ToolRegistryService 升级至 v4 ToolDefinition
5. SchedulerService / WorldDataService / CommandService 线程调度审计
6. UI 改造：流式渲染 + Persona 注入
7. PersistenceManager 最终持久化集成
8. 全局异常体系补全 & OrchestrationService 智能错误处理

> 注：详细任务拆分与实时状态以 `TODO.md` / GitHub Projects 看板为准。