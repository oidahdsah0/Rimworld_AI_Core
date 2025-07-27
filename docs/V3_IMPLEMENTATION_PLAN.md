# RimAI Core v3.0 - 重构施工计划与清单

本文档旨在作为 v3.0 架构重构工作的核心指导蓝图，确保开发过程清晰、有序、可追踪。

---

## 1. 最终目录结构规划 (Target Directory Structure)

一个清晰、有预见性的目录结构是项目能保持整洁的基石。我们将遵循“关注点分离”和“面向接口编程”的核心原则，规划 `Source/` 目录如下：

```
RimAI.Core/
└── Source/
    ├── Contracts/
    │   ├── Services/      # (契约) 所有服务接口 (如 ILLMService.cs)
    │   ├── Data/          # (契约) 数据模型/DTOs/配置模型 (如 CoreConfig.cs)
    │   ├── Events/        # (契约) 事件接口 (IEvent.cs) 和具体事件类
    │   └── Tools/         # (契约) 工具接口 (IRimAITool.cs)
    │
    ├── Architecture/
    │   ├── DI/            # (架构) 依赖注入核心 (ServiceContainer.cs, CoreServices.cs)
    │   ├── Scheduling/    # (架构) 线程调度器 (SchedulerService.cs)
    │   └── Caching/       # (架构) 缓存服务 (CacheService.cs)
    │
    ├── Services/
    │   │                  # (实现) 所有服务接口的具体实现类
    │   ├── LLMService.cs
    │   ├── OrchestrationService.cs
    │   └── ...
    │
    ├── Lifecycle/
    │   ├── RimAIMod.cs    # (生命周期) Mod主入口，负责初始化
    │   └── PersistenceManager.cs # (生命周期) 存读档的游戏组件钩子
    │
    ├── Tools/
    │   │                  # (实现) 所有工具接口的具体实现类
    │   ├── GetColonyStatusTool.cs
    │   └── ...
    │
    ├── UI/
    │   ├── MainTabWindow_RimAI.cs
    │   ├── Dialog_PawnConversation.cs
    │   └── RimAISettingsWindow.cs
    │
    ├── Events/
    │   └── GameEventHooks.cs  # (事件) 监听原生游戏事件并发布到我们总线的钩子
    │
    └── Exceptions/
        │                      # (异常) 自定义异常类
        ├── LLMException.cs
        └── ToolExecutionException.cs
```

### 核心目录职责：

*   **`Contracts/`**: **项目的基石**。定义所有服务接口、数据模型和事件契约。项目的其他部分应只依赖于此目录中的类型，这是解耦的关键。
*   **`Architecture/`**: 存放与具体业务无关的**技术基础设施**，如DI容器、调度器、缓存等。
*   **`Services/`**: 存放`Contracts/Services/`目录下所有接口的**具体实现**。
*   **`Lifecycle/`**: 负责Mod的启动、初始化和存读档生命周期管理。
*   **`Tools/`**: AI所有“能力”的具体实现。
*   **`UI/`**: 所有与玩家交互的界面代码。
*   **`Events/`**: 监听并转换游戏原生事件的“钩子”。
*   **`Exceptions/`**: 自定义的、有业务含义的异常类。

---

## 2. 施工计划：五阶段实施策略 (Phased Implementation)

我们将采用“由内而外，由下至上”的策略，分五个阶段完成重构，确保每一步都有可验证的成果。

*   **阶段一：奠定基石 (The Foundation)**
    *   **目标：** 搭建应用程序的骨架和配置系统。
    *   **产出：** 一个可以运行的、空的框架，具备服务管理和配置加载的核心能力。

*   **阶段二：连接外部世界 (The I/O Layer)**
    *   **目标：** 打通与 LLM API 和 RimWorld 游戏引擎的安全通信管道。
    *   **产出：** 具备核心的I/O能力。可以安全地读写游戏数据，并与LLM通信。

*   **阶段三：构建核心业务能力 (The Core Logic)**
    *   **目标：** 构建为“大脑”提供支持的专业领域服务。
    *   **产出：** AI具备了“思考”所需的所有素材，能组装包含上下文的复杂请求。

*   **阶段四：激活大脑与整合 (The Brain & Integration)**
    *   **目标：** 实现核心编排逻辑，将所有服务串联起来。
    *   **产出：** 一个能与玩家进行工具辅助型对话的、真正意义上的智能AI。

*   **阶段五：健壮性、持久化与个性化 (Refinement & Polish)**
    *   **目标：** 添加高级功能，让系统变得更强大、更可靠、更具个性。
    *   **产出：** 一个功能完整、稳定且高度可扩展的最终产品。

---

## 3. 详细施工清单 (Implementation Checklist)

将上述计划分解为可追踪的具体任务。

### ✅ 阶段零：项目初始化

- [ ] 清理 `Source/` 目录下的旧文件（或将其移动到 `Source/Old/` 备份）。
- [ ] 根据规划创建新的空目录结构。

### ✅ 阶段一：奠定基石 (The Foundation)

-   **DI 核心**
    - [ ] `Architecture/DI/ServiceContainer.cs`: 实现服务注册 `Register<T, TImpl>()` 和解析 `Resolve<T>()` 功能。
    - [ ] `Architecture/DI/CoreServices.cs`: 创建静态服务门面，用于从无法使用构造函数注入的地方（如UI）访问服务。
-   **配置服务**
    - [ ] `Contracts/Data/CoreConfig.cs`: 定义强类型的配置数据模型。
    - [ ] `Contracts/Services/IConfigurationService.cs`: 定义配置服务接口，**包括 `Current` 属性、`Reload()` 方法和 `OnConfigurationChanged` 事件**。
    - [ ] `Services/ConfigurationService.cs`: 实现从 `ModSettings` 加载配置的逻辑，**并完整实现热重载机制**。
-   **生命周期**
    - [ ] `Lifecycle/RimAIMod.cs`: 在构造函数中初始化 `ServiceContainer`。
    - [ ] `Lifecycle/RimAIMod.cs`: 注册 `IConfigurationService` 和 `ServiceContainer` 自身。
    - [ ] `Lifecycle/RimAIMod.cs`: 将容器实例赋给 `CoreServices` 的静态属性。

### ✅ 阶段二：连接外部世界 (The I/O Layer)

-   **底层工具**
    - [ ] `Contracts/Services/ISchedulerService.cs`: 定义接口。
    - [ ] `Architecture/Scheduling/SchedulerService.cs`: 实现基于 `GameComponent` 的主线程调度逻辑。
    - [ ] `Contracts/Services/ICacheService.cs`: 定义接口。
    - [ ] `Architecture/Caching/CacheService.cs`: 实现基于 `Dictionary` 的线程安全内存缓存。
-   **LLM 网关**
    - [ ] `Contracts/Services/ILLMService.cs`: 定义接口 (`GetResponseAsync`, `StreamResponseAsync` 等)。
    - [ ] `Services/LLMService.cs`: 实现对 `RimAI.Framework.API` 的基本调用封装。
    - [ ] `Services/LLMService.cs`: 注入并使用 `ICacheService` 和 `IConfigurationService`。
-   **游戏世界防腐层**
    - [ ] `Contracts/Services/IWorldDataService.cs`: 定义安全的“读”接口。
    - [ ] `Services/WorldDataService.cs`: 实现，内部强制使用 `ISchedulerService`。
    - [ ] `Contracts/Services/ICommandService.cs`: 定义安全的“写”接口。
    - [ ] `Services/CommandService.cs`: 实现，内部强制使用 `ISchedulerService`。
-   **注册服务**
    - [ ] `Lifecycle/RimAIMod.cs`: 在启动时注册所有本阶段完成的服务。

### ✅ 阶段三：构建核心业务能力 (The Core Logic)

-   **工具系统**
    - [ ] `Contracts/Tools/IRimAITool.cs`: 定义工具接口 (`Name`, `Description`, `GetSchema`, `ExecuteAsync`)。
    - [ ] `Contracts/Services/IToolRegistryService.cs`: 定义工具注册器接口。
    - [ ] `Services/ToolRegistryService.cs`: 实现自动发现和注册 `IRimAITool` 的逻辑。
    - [ ] `Tools/`: 创建至少一个示例工具 (如 `GetColonyStatusTool`) 进行测试。
-   **提示词工厂**
    - [ ] `Contracts/Services/IPromptFactoryService.cs`: 定义接口 (`BuildPromptAsync`)。
    - [ ] `Services/PromptFactoryService.cs`: 实现，注入并使用 `IWorldDataService`, `IToolRegistryService` 等。
-   **历史服务**
    - [ ] `Contracts/Data/Conversation.cs`: 定义对话和条目的数据模型。
    - [ ] `Contracts/Services/IHistoryService.cs`: 定义接口 (`RecordEntryAsync`, `GetHistoryAsync`)。
    - [ ] `Services/HistoryService.cs`: 实现双索引内存存储。
-   **注册服务**
    - [ ] `Lifecycle/RimAIMod.cs`: 注册所有本阶段完成的服务。

### ✅ 阶段四：激活大脑与整合 (The Brain & Integration)

-   **编排服务**
    - [ ] `Contracts/Services/IOrchestrationService.cs`: 定义核心接口 (`ExecuteToolAssistedQueryAsync`)。
    - [ ] `Services/OrchestrationService.cs`: 实现完整的5步工具调用工作流。
    - [ ] `Services/OrchestrationService.cs`: 注入并调用几乎所有三阶段及以前的服务。
-   **UI 对接**
    - [ ] `UI/`: 创建一个临时的调试窗口或主界面标签页。
    - [ ] UI 代码通过 `CoreServices.OrchestrationService` 调用核心方法。
    - [ ] 实现一个简单的文本框用于输入，一个区域用于显示流式输出。
-   **注册服务**
    - [ ] `Lifecycle/RimAIMod.cs`: 注册 `IOrchestrationService`。

### ✅ 阶段五：健壮性、持久化与个性化 (Refinement & Polish)

-   **持久化**
    - [ ] `Contracts/Services/IPersistenceService.cs`: 定义接口。
    - [ ] `Services/PersistenceService.cs`: 实现，内部使用 `Scribe` API。
    - [ ] `Lifecycle/PersistenceManager.cs`: 创建 `GameComponent`，在 `ExposeData` 时调用 `IPersistenceService`。
    - [ ] `Services/HistoryService.cs`: 添加 `GetState()` 和 `LoadState()` 方法供持久化服务调用。
-   **事件处理**
    - [ ] `Contracts/Events/IEvent.cs`: 定义事件接口。
    - [ ] `Contracts/Services/IEventBus.cs`: 定义底层总线接口。
    - [ ] `Services/EventBus.cs`: 实现简单的发布订阅。
    - [ ] `Contracts/Services/IEventAggregatorService.cs`: 定义智能聚合器接口。
    - [ ] `Services/EventAggregatorService.cs`: 实现定时、节流、聚合逻辑。
-   **韧性**
    - [ ] `Exceptions/`: 创建所有自定义异常类。
    - [ ] `Services/LLMService.cs`: 添加重试和熔断逻辑。
    - [ ] `Services/OrchestrationService.cs`: 添加 `try-catch` 块，实现对 `ToolExecutionException` 的智能反馈处理。
-   **个性化**
    - [ ] `Contracts/Data/Persona.cs`: 定义人格数据模型。
    - [ ] `Contracts/Services/IPersonaService.cs`: 定义接口。
    - [ ] `Services/PersonaService.cs`: 实现从 Defs 加载人格模板的逻辑。
    - [ ] `UI/`: 修改UI层，使其在调用 `IOrchestrationService` 时传入 `Persona` 的系统提示词。 

---

## 施工日志

- **2025-07-25 (阶段一):** 完成了整个“奠定基石”阶段。DI容器 (`ServiceContainer`, `CoreServices`)、配置系统 (`IConfigurationService` 及其实现) 和Mod生命周期入口 (`RimAIMod`) 均已完成并注册，项目核心骨架搭建完毕。
- **2025-07-26 (阶段二):** “连接外部世界”阶段启动。已完成 `ISchedulerService`, `ICacheService` 的接口和实现。在实现 `ILLMService` 时，识别出 `RimAI.Framework` 的API存在耦合问题。
- **2025-07-26 (决策):** Core 模块开发暂停。**优先重构 Framework 模块的公共 API**，以实现更好的分层解耦。Core 模块将等待 Framework 新版 API 发布后再继续开发。 