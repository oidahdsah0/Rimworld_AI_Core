# RimAI.Core V3.0 架构设计文档

## 1. 核心设计哲学

本架构遵循以下核心原则：

- **单一职责原则 (SRP):** 每个类或服务都应该只有一个改变的理由。
- **高内聚, 低耦合 (High Cohesion, Low Coupling):** 相关的功能被组织在一起，而模块间的依赖则最小化。
- **面向接口编程 (Programming to an Interface):** 依赖于抽象（接口）而非具体实现，以提高灵活性和可测试性。
- **依赖倒置原则 (DIP):** 通过依赖注入（DI）和服务容器来管理服务生命周期和依赖关系，实现控制反转 (IoC)。

我们的目标是构建一个健壮、可扩展、可维护且高度可测试的 `Core` 模块。

---

## 2. 核心服务详解

### 2.1. ServiceContainer & CoreServices (依赖注入核心)

`ServiceContainer` 和 `CoreServices` 共同构成了我们架构的依赖注入 (DI) 核心。理解它们各自的职责和它们之间的关系至关重要。

#### 2.1.1. ServiceContainer (服务容器)

**角色与定位：**

`ServiceContainer` 是整个 `Core` 模块的**心脏和中央装配线**。它遵循标准的**依赖注入容器 (DI Container)** 模式，其唯一职责是**管理所有服务的生命周期和依赖关系**。

**核心职责 (反思与改进):**

1.  **服务注册 (Service Registration):**
    -   提供 `Register<TInterface, TImplementation>()` 方法，用于在程序启动时声明所有可用的服务。
    -   所有核心服务都将被注册为**单例 (Singleton)**，确保在整个应用生命周期中只有一个实例。

2.  **自动依赖解析与注入 (Automatic Dependency Resolution & Injection):**
    -   **[新架构核心改进]** 与旧设计的手动接线不同，新的 `ServiceContainer` **必须**实现真正的**构造函数注入 (Constructor Injection)**。
    -   当容器创建一个服务实例时，它会自动分析该服务类的构造函数，并**自动地、递归地**创建并传入其所需的所有依赖服务。开发者不再需要手动管理服务间的依赖关系。

3.  **架构纪律的强制执行 (Architectural Discipline Enforcement):**
    -   任何未在容器中注册的服务或其依赖项，在程序启动阶段就会导致“快速失败” (Fail-Fast)，从而强制保证了架构的完整性。
    -   **所有核心服务，都必须在 `ServiceContainer` 中注册。** 这是不可动摇的架构铁律。
    -   **[重点示例] 以 `LLMService` 为例：** 将 `ILLMService` 接口与其实现类在容器中注册，是保证其作为“唯一网关”角色的技术前提。任何需要与LLM通信的服务（如 `OrchestrationService`），都将通过构造函数声明其对 `ILLMService` 接口的依赖。容器在创建 `OrchestrationService` 时，会自动将那个唯一的、正确的 `LLMService` 实例注入进去。这从根本上杜绝了绕过网关、违反架构原则的可能性。

#### 2.1.2. CoreServices (静态服务门面)

**角色与定位 (与旧设计的关键区别):**

`CoreServices` 是一个静态类，扮演**服务定位器 (Service Locator)** 的角色。然而，在我们新的 v3.0 架构中，它的使用受到了**严格的限制**，以避免旧设计中依赖关系被隐藏的弊端。

**使用原则：**

1.  **构造函数注入为首选 (Constructor Injection is the Default):**
    -   **99% 的情况下，服务应该通过构造函数来获取其依赖。** 例如, `OrchestrationService` 的构造函数应该是 `public OrchestrationService(ILLMService llmService, IToolService toolService)`。这是最清晰、最可测试的方式。

2.  **`CoreServices` 的适用场景 (When to Use `CoreServices`):**
    -   **[新架构核心原则]** `CoreServices` **仅**用于那些**无法或极难使用构造函数注入**的特殊场景。
    -   **唯一合法用例：在 RimWorld 游戏对象 (如 `GameComponent`, `MainTabWindow`, `ThingComp` 等) 的非构造函数方法中**。因为这些对象的生命周期由 RimWorld 游戏引擎管理，我们无法控制它们的构造过程，因此无法向其构造函数中注入服务。在这些类的`PostInit`、`DoWindowContents`或自定义方法中，使用 `CoreServices.LLMService` 是允许的。
    -   **严禁在普通服务类 (`OrchestrationService`, `PawnAnalyzer` 等) 的内部使用 `CoreServices`。** 这些类必须使用构造函数注入。

**结论：**

我们通过明确的职责分离，修正了旧设计的核心缺陷。`ServiceContainer` 负责“幕后”的自动装配，而 `CoreServices` 则为特定场景提供了一个受限制的“前台”访问点，确保了我们新架构的清晰性、健壮性和可测试性。

---

### 2.2. `ILLMService` (大语言模型服务)

#### 2.2.1. 角色与定位 (Role & Position)

`LLMService` 是 **`Core` 模块与 `Framework` 模块之间所有大语言模型 (LLM) 通信的唯一、强制性的网关**。

在我们的架构中，`RimAI.Framework` 提供的 `RimAIApi` 是一个强大但“低级”的引擎。直接在 `Core` 模块的各个业务服务中调用 `RimAIApi` 会导致策略逻辑分散、代码重复、耦合紧密以及测试困难。

因此，`LLMService` 的存在并非“重复造轮子”，而是为强大的“引擎” (`RimAIApi`) 构建一个高度集成和智能化的“**驾驶舱**”。`Core` 模块中的任何其他服务（如 `OrchestrationService`, `Analyzers` 等）都**严禁**直接访问 `RimAIApi`，它们必须、也只能通过 `LLMService` 来与 LLM 进行交互。

#### 2.2.2. 核心职责 (Responsibilities)

1.  **抽象与封装 (Abstraction & Encapsulation):**
    -   完全隐藏 `RimAI.Framework.API` 的实现细节。
    -   `LLMService` 将是 `Core` 模块中唯一允许 `using RimAI.Framework.API;` 的地方。
    -   将 `Framework` 的数据模型（如 `LLMRequestOptions`）转换为 `Core` 内部的模型，避免“依赖泄漏”。

2.  **统一策略实施 (Centralized Policy Enforcement):**
    -   **缓存 (Caching):** 在此服务中实现统一的 LLM 请求缓存策略，减少不必要的API调用和成本。
    -   **日志与监控 (Logging & Monitoring):** 作为所有 LLM 流量的必经之路，提供集中的日志记录、性能分析和错误追踪。
    -   **重试与熔断 (Retry & Circuit Breaking):** 实现统一的、智能的请求重试机制，并在 `Framework` 持续失败时提供熔断保护。
    -   **全局参数配置 (Global Configuration):** 提供一个中心点来管理和注入全局LLM参数（如安全设置、全局 Temperature、TopP 等）。

3.  **简化API契约 (Simplified API Contract):**
    -   向 `Core` 模块内部暴露一个更简洁、更符合内部业务需求的接口 (`ILLMService`)。

4.  **提升可测试性 (Enhanced Testability):**
    -   由于内部服务依赖于 `ILLMService` 接口，我们可以轻易地在单元测试中注入一个模拟的 (Mock) `LLMService`，从而实现快速、可靠且独立的业务逻辑测试。

**结论：** `ILLMService` 作为 `Core` 模块与 `Framework` 模块通信的唯一网关，其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它的存在保证了架构的解耦、可控和可测试性。

---

### 2.3. 数据安全访问层 (Safe Data Access Layer)

与游戏主循环（主线程）的数据交互是 Mod 开发中最危险、最容易引发崩溃的环节。为了从架构层面根除此类问题，v3.0 引入了一个全新的、职责分离的数据安全访问层。它由 `ISchedulerService` 和 `IWorldDataService` 两个核心服务构成，取代了旧版设计中职责混乱的 `SafeAccessService`。

#### 2.3.1. `ISchedulerService` (主线程调度器)

**角色与定位：**

`ISchedulerService` 是一个**底层的、纯粹的技术服务**。它的唯一职责，就是提供一个机制，能将一个委托（`Action` 或 `Func`）从**任何后台线程**安全地、可靠地调度到 **RimWorld 的主线程**上执行。

**核心实现：**

该服务内部会维护一个线程安全的任务队列。任何需要回到主线程执行的任务都会被提交到这个队列中。与此同时，一个在主线程上运行的 `GameComponent` 会在每个游戏更新周期 (`Update` 方法)中检查此队列，并执行其中的任务。这是在 RimWorld 中进行跨线程调度的**标准且唯一正确**的模式。

**核心职责：**

1.  **线程安全调度：** 提供 `ScheduleOnMainThread(Action action)` 和 `ScheduleOnMainThreadAsync<T>(Func<T> function)` 等方法，作为后台任务与主线程沟通的桥梁。
2.  **隐藏实现细节：** 将复杂的线程同步逻辑完全封装，调用者无需关心其内部实现。

**使用原则：**

`ISchedulerService` 是一个底层工具。**上层的业务服务（如 `OrchestrationService`）应该极力避免直接使用它**。直接使用它意味着业务逻辑中混入了线程管理的职责，这违反了关注点分离的原则。它的主要消费者是 `IWorldDataService`。

**结论：** `ISchedulerService` 是一个底层的、纯粹的技术服务，它**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它的主要消费者是 `IWorldDataService`。

#### 2.3.2. `IWorldDataService` (世界数据服务)

**角色与定位：**

`IWorldDataService` 是 **`Core` 模块内部所有服务获取实时、易变的 RimWorld 游戏世界数据的唯一、强制性入口**。它是一个典型的**防腐层 (Anti-Corruption Layer)**，其核心使命是保护 `Core` 模块的业务逻辑不被 RimWorld API 的复杂性和线程安全性所“污染”。

**核心实现：**

`IWorldDataService` 的所有方法都将在内部调用 `ISchedulerService`，以确保所有对 `Verse` 命名空间下API的访问都发生在主线程上。它将所有不安全的、直接的游戏数据交互代码**全部封装在自己内部**。

**核心职责：**

1.  **提供稳定的数据契约：** 定义一系列清晰、稳定的异步方法（如 `GetPawnSummaryAsync`, `GetColonyResourcesAsync`），向业务层提供干净、格式化好的游戏数据模型。
2.  **线程安全保证：** 内部处理所有的线程切换逻辑，使得调用者可以像调用一个普通的Web API一样，从任何线程发起数据请求，而无需担心线程安全问题。
3.  **异常处理与屏蔽：** 捕获在访问游戏数据时可能发生的各种异常（如对象不存在、游戏未加载等），并将其转换为业务层可以理解的、稳定的异常类型或返回安全的默认值。

**结论：** `IWorldDataService` 是保护 `Core` 模块不被 RimWorld API 污染的关键防腐层。其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它使得上层业务逻辑可以安全、优雅地访问游戏数据。

---

### 2.4. 智能事件处理架构 (Intelligent Event Handling Architecture)

为了避免游戏内的高频事件（如战斗日志、物品损坏等）无意义地、不受控制地冲击LLM API，v3.0 引入了一个全新的、带节流和优先级处理能力的**三层事件处理模型**。它取代了旧版设计中那个简单的、无状态的 `EventBus`。

#### 2.4.1. `IEvent` (事件契约)

事件契约是整个系统的基础。我们定义了一个 `IEvent` 接口，所有具体的事件（如“袭击到达”、“作物歉收”）都必须实现它。这个设计的核心是为每个事件赋予一个**优先级**，这是后续智能处理的依据。

**定位：**
-   这是一个纯粹的**数据契约 (Data Contract)**，定义了事件的结构。
-   在文件组织上，`IEvent` 接口以及所有具体的事件类，都应被放置在 `Source/Events/` 目录下，遵循“一个公共类型一个文件”的最佳实践。

**核心设计：**
```csharp
public enum EventPriority { Low, Medium, High, Critical }

public interface IEvent
{
    string Id { get; }
    DateTime Timestamp { get; }
    EventPriority Priority { get; }
    string Describe(); // 用于生成该事件的简短文本描述
}
```

#### 2.4.2. `IEventBus` (底层事件总线)

**角色与定位：**
`IEventBus` 是一个**底层的、无状态的、快速的事件传递通道**。它的职责非常单一：接收游戏各处发来的 `IEvent` 实例，并立即将其广播给所有订阅者。它不进行任何过滤、缓存或逻辑处理。

**定位：**
-   这是一个基础的**服务契约 (Service Contract)**，提供了发布-订阅的能力。
-   它好比是城市的“原始供水管网”，只负责输送，不负责处理。

**结论：** `IEventBus` 是一个基础的、无状态的发布-订阅服务，它**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。**

#### 2.4.3. `IEventAggregatorService` (智能事件聚合器)

**角色与定位：**
这是新架构的核心创新。它是一个**高级的、有状态的、带定时逻辑的服务**。它的职责是作为 `IEventBus` 的订阅者，对接收到的事件进行**收集、过滤、排序、批量化**，并最终以**受控的、节制的**频率与 `ILLMService` 交互。

**核心工作流程：**

1.  **订阅与积累：** 服务启动时订阅 `IEventBus`。每当接收到一个事件，它只是将其存入一个内部的待处理事件列表，不立即做任何事。
2.  **定时节流：** 服务内部有一个可配置的计时器（例如10分钟）。只有当计时器触发，并且API冷却期已过时，才会进入处理流程。
3.  **优先级处理：**
    -   检查待处理事件列表。若列表为空或只包含低优先级事件，则认为“无重要事项发生”，跳过本次LLM请求。
    -   若有重要事件，则对列表按优先级进行**降序排序**，并**截断**以获取最重要的前N个事件。
4.  **批量请求与冷却：**
    -   将截断后的事件列表聚合成一个**单一的、条理清晰的提示词**，通过 `ILLMService` 发起**一次** LLM 请求。
    -   请求一旦发出，立刻**启动API冷却计时器**（例如10分钟），并在冷却期内拒绝新的处理请求。
    -   清空待处理事件列表。

**结论：** `IEventAggregatorService` 是实现对高频事件进行智能管理的核心大脑。其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它将事件的定义、传递和智能处理彻底分离。

---

### 2.5. `IPromptFactoryService` (提示词工厂服务)

如果说 `ILLMService` 是与 LLM 通信的“电话线”，那么 `IPromptFactoryService` 就是在打电话前负责**撰写演讲稿、整理会议纪要、并附上所有背景材料的“首席秘书”**。它是连接我们内部数据和外部智能的**总翻译官**和**情报汇总官**。

#### 2.5.1. 角色与定位

`PromptFactoryService` 是 `Core` 模块中**所有** LLM 请求的**强制性前置处理器**。它的定位是“**提示词的中央厨房**”。

-   **唯一职责：** 接收一个**意图 (Intent)** 和相关的**上下文 (Context)**，然后调用所有必要的内部服务（如 `IWorldDataService`）来获取最新鲜的数据，最终组装并返回一个结构化、信息完备的**提示词负载 (Prompt Payload)**。
-   **单一职责原则：** 它**只负责组装**，绝不负责发送。它的输出，是 `ILLMService` 的输入。
-   **智能上下文聚合器：** 它不是一个简单的字符串替换模板引擎。它是一个智能的聚合器，知道为了满足某个提示词“配方”，需要去调用哪些服务来获取动态数据。

#### 2.5.2. 核心职责

1.  **上下文聚合 (Context Aggregation):**
    -   作为 `IWorldDataService` 的主要消费者，根据提示词模板的需求，异步获取实时的游戏世界数据（如天气、资源、殖民者状态等）。
    -   消费 `IHistoryService`（未来的对话历史服务）来获取相关的对话上下文。
    -   将用户的原始查询、动态获取的游戏数据、相关的历史对话、以及固定的系统指令（如AI的角色扮演指令）聚合在一起。

2.  **模板化组装 (Template-based Assembly):**
    -   服务内部会管理一系列提示词“**配方 (Recipe)**”。每个配方都会声明它需要哪些动态数据。例如，“总督每日晨报”配方，会声明它需要“天气信息”、“资源概览”和“所有殖民者的心情摘要”。

3.  **用户内容注入 (User Content Injection):**
    -   服务的调用者（主要是 `OrchestrationService`）可以传递一个上下文对象，其中包含用户指定的、需要被特别强调或包含在提示词中的内容，实现了高度的灵活性。

4.  **标准化输出 (Standardized Output):**
    -   它的最终输出**不是一个简单的字符串**，而是一个结构化的 `PromptPayload` 对象 (或直接是 `List<ChatMessage>`)。这个对象会清晰地划分系统指令、历史对话、用户提问和注入的上下文，完全符合现代 LLM API 的输入格式，为 `ILLMService` 提供“弹药”。

#### 2.5.3. 核心工作流程

1.  **发起方 (`OrchestrationService`)** 创建一个 `PromptBuildConfig` 对象，指定一个“配方”和一些附加上下文，然后调用 `promptFactory.BuildPromptAsync(config)`。
2.  **处理方 (`PromptFactoryService`)** 接收到“订单”，根据配方要求，**并行地、异步地**向 `IWorldDataService` 等服务请求所需数据。
3.  在所有数据返回后，工厂将所有信息（系统指令、动态数据、历史、用户附加上下文等）组装成一个结构化的 `List<ChatMessage>`。
4.  **接收方 (`OrchestrationService`)** 接收到这个“信息包”，并将其直接传递给 `ILLMService` 以执行最终的 LLM 请求。

**结论：** `IPromptFactoryService` 优雅地解决了将分散的内部数据转化为信息完备的LLM请求这一核心问题。其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它使得上层业务逻辑可以用高级、面向意图的语言与LLM交互。

---

### 2.6. `ICacheService` (缓存服务)

**角色与定位：**

`ICacheService` 是一个**专用的、高性能的内存缓存服务**。它在我们的架构中扮演着“**LLM 请求防火墙**”的关键角色，其核心使命是**系统稳定性和成本控制**，其次才是性能优化。

-   **独占消费者：** 为了严格执行“只有请求会进入缓存”的架构纪律，`ICacheService` 被设计为**只被 `ILLMService` 独家依赖和使用**。任何其他服务都严禁直接访问它。
-   **实现方式：** 内部将使用一个线程安全的 `Dictionary` 来实现。缓存的键(Key)由请求的最终提示词内容生成（例如通过SHA256哈希），缓存的值(Value)则包含响应内容和过期时间戳。

**核心工作流程 (LLM 防火墙):**

这个流程被强制嵌入在 `LLMService` 的实现中：
1.  `LLMService` 在收到任何请求后，首先根据请求内容生成一个唯一的**缓存键**。
2.  使用此键调用 `ICacheService.TryGetValue()`。
3.  **缓存命中 (Cache Hit):** 若找到未过期的缓存，`LLMService` **立即返回缓存的响应**，API 调用被成功拦截，流程结束。
4.  **缓存未命中 (Cache Miss):** 若未找到缓存，`LLMService` 才会继续调用 `Framework` 的API。
5.  在从 `Framework` 成功获取响应后，`LLMService` 会将新响应通过 `ICacheService.Set()` 存入缓存，并设置一个合理的过期时间（例如5分钟）。

**结论：** 该设计通过将缓存逻辑强制性地、唯一地嵌入到 `ILLMService` 的处理流程中，确保了LLM请求的“防火墙”机制。`ICacheService` 的实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。**

---

### 2.7. 持久化架构 (Persistence Architecture)

为了实现一个干净、可测试、职责分离的持久化方案，v3.1 架构采用了一个全新的“**发令员-总工程师-专家**”模型。此模型修正了旧版设计中业务服务（如`HistoryService`）与持久化逻辑（`IExposable`接口）不当耦合的问题。

#### 2.7.1. 核心组件与职责

1.  **`PersistenceManager` (`GameComponent`) - 发令员:**
    -   **角色:** 这是一个继承自 `GameComponent` 的、极其轻量的类。
    -   **唯一职责:** 作为 RimWorld 存读档生命周期的“发令员”。当游戏引擎调用其 `ExposeData()` 方法时，它自己不进行任何数据读写，而是立刻调用 `IPersistenceService` 的相应方法，指令其开始工作。

2.  **`IPersistenceService` - 总工程师:**
    -   **角色:** 负责**所有**数据持久化**技术实现**的“总工程师”。
    -   **唯一职责:** 它是 `Core` 模块中唯一允许 `using Verse.Scribe;` 的持久化服务。所有 `Scribe.Look(...)` 调用都**只存在于这个服务内部**。它为需要持久化的业务服务提供具体的、强类型的方法，如 `PersistHistoryState(IHistoryService historyService)`。

3.  **需要持久化的业务服务 (如 `IHistoryService`) - 专家:**
    -   **角色:** 这些是纯粹的业务服务专家，例如“历史学家”。
    -   **核心变化:** 它们**不再实现** RimWorld 特有的 `IExposable` 或我们自定义的 `IPersistable` 接口。它们回归了纯粹的业务逻辑，只需提供一些`public`方法，允许“总工程师”(`IPersistenceService`) 在需要时获取或设置其内部状态即可。

#### 2.7.2. 工作流程

1.  **依赖注入:** `ServiceContainer` 在启动时，会将 `IPersistenceService`（总工程师）和所有需要被持久化的服务（如`IHistoryService`）的实例，注入到 `PersistenceManager`（发令员）中。
2.  **存档开始:** 游戏引擎调用 `PersistenceManager.ExposeData()`。
3.  **专业分工:** `PersistenceManager` 调用 `_persistenceService.PersistHistoryState(_historyService)`。`IPersistenceService` 随即从 `_historyService` 获取需要保存的状态数据，并使用 `Scribe` 系统完成具体的读写工作。

**结论:**
这个架构通过三层职责分离，实现了完美的关注点分离。`PersistenceManager` 和 `IPersistenceService` 的实现类都**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。**

---

### 2.8. `IHistoryService` (认知历史服务)

`IHistoryService` 在 v3.0 架构中，其重要性被提升到了战略高度。它不再是一个简单的“聊天记录器”，而是被设计为 **AI 的“海马体”和长期记忆中心**。它负责捕捉、存储和检索对话上下文，为 AI 提供连贯的叙事和深刻的人际关系理解能力。**所有对话历史都代表着玩家的宝贵记忆，因此，本服务的状态必须被完整地、可靠地持久化到游戏存档中。**

#### 2.8.1. 核心设计原则：认知逻辑与数据独立

我们遵循一个核心的认知逻辑原则：**直接参与的对话是“记忆”，共同在场的对话是“背景”**。为了实现这一目标，我们采用了以下设计：

1.  **账本独立性：** 任何一组参与者（无论是 `[A, B]` 还是 `[A, B, C]`）都对应一个**完全独立的对话账本 (`Conversation`)**。账本之间的数据互不包含。
2.  **唯一ID索引：** 
    -   **[核心技术决策]** 为了应对玩家和NPC可能改名、改变派系等情况，所有在 `HistoryService` 中代表参与者的ID，**必须**使用其稳定且唯一的内部ID。
    -   **Pawn (殖民者/NPC):** 必须使用 `pawn.ThingID`。
    -   **玩家 (Player):** 必须使用 `Faction.OfPlayer.GetUniqueLoadID()` 或一个内部等价的特殊标识符（如 `__PLAYER__`）。
    -   每个对话账本的唯一ID，由这些**稳定ID排序后**组合而成。严禁使用易变的显示名称作为索引。
3.  **查询时智能附加：** `HistoryService` 的真正威力体现在其查询功能上。当请求一个二人对话（如 `[A, B]`）的历史时，服务不仅会返回他们**精确匹配**的主线对话，还会**智能地搜索并附加**所有包含了这两人的多人对话（如 `[A, B, C]`），作为补充的“背景材料”。

#### 2.8.2. 内部实现：双索引存储引擎

为了在实现上述“智能附加”查询的同时，保证**闪电般的查询速度**，避免因遍历所有历史而导致的性能雪崩，`HistoryService` 的内部实现**必须**采用**双索引存储引擎**。这是一个实现细节，被完美地封装在服务内部，对调用者透明。

1.  **主存储 (Primary Store) - `Dictionary<string, Conversation>`:**
    -   **键 (Key):** 唯一的、由排序参与者生成的会话ID。
    -   **值 (Value):** 完整的 `Conversation` 对象。
    -   **职责：** 提供对指定对话的 `O(1)` 复杂度的快速访问。

2.  **倒排参与者索引 (Inverted Participant Index) - `Dictionary<string, HashSet<string>>`:**
    -   **键 (Key):** 单个参与者的ID。
    -   **值 (Value):** 一个包含该参与者所有会话ID的 `HashSet`。
    -   **职责：** 这是实现高性能包含式搜索的**核心**。它允许服务通过集合运算（如交集、并集），瞬间找到所有与指定参与者相关的对话，而无需遍历整个数据集。

#### 2.8.3. 核心职责

1.  **对话记录 (`RecordEntryAsync`):**
    -   接收一个新的对话条目 (`ConversationEntry`) 和参与者列表。
    -   安全地（通过`ISchedulerService`）获取当前的游戏时间戳。
    -   找到或创建对应的 `Conversation` 账本，将新条目加入。
    -   同步更新主存储和倒排索引，确保两个索引始终保持一致。

2.  **历史检索 (`GetHistoryAsync`):**
    -   接收一个查询请求（包含参与者列表）。
    -   利用**倒排索引**通过集合交集运算，高效地找出所有相关对话的ID。
    -   根据ID从**主存储**中提取完整的 `Conversation` 对象。
    -   将结果区分为**主线历史**（精确匹配）和**背景历史**（包含匹配），并封装成一个结构化的 `HistoricalContext` 对象返回。

3.  **状态管理 (State Management for Persistence):**
    -   **[新架构核心修正]** `HistoryService` **不再实现** `IExposable` 或 `IPersistable` 接口。
    -   它将提供 `public` 的方法，如 `GetStateForPersistence()` 和 `LoadStateFromPersistence(HistoryState state)`，允许 `IPersistenceService` 在不侵入其内部逻辑的情况下，安全地获取和恢复其状态（包含主存储和倒排索引两个字典）。

#### 2.8.4. 记录原则：调用者无关性与最终结果唯一性

这是 `IHistoryService` 最核心、最不可动摇的架构纪律，必须被所有调用者严格遵守。

1.  **调用者无关性 (Caller Agnostic):**
    -   `IHistoryService` 对其调用者一无所知，也毫不关心。无论是底层的 `ILLMService` 为一个简单问答场景记录对话，还是高级的 `IOrchestrationService` 为一个复杂工作流记录最终结果，对于 `IHistoryService` 来说，它们都只是在调用 `RecordEntryAsync` 方法而已。

2.  **最终结果唯一性 (Final-Result-Only Principle):**
    -   **[核心架构铁律]** `IHistoryService` 只负责记录**对最终用户有意义的、作为“对话”一部分的输入和输出**。
    -   **调用者有绝对的责任**来保证只将以下内容传入 `RecordEntryAsync` 方法：
        -   用户的**初始输入/问题**。
        -   AI针对该问题返回的**最终自然语言回复**。
    -   所有中间过程，如AI的内心独白、工具调用决策、API返回的原始JSON数据、函数执行结果等，都**严禁**作为“对话历史”被记录。这些过程信息应该被视为临时的、用于调试的日志，而不是玩家的“宝贵记忆”。

**结论：** `IHistoryService` 的设计，完美地平衡了**业务逻辑的清晰性**和**底层实现的性能效率**。其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。** 它是构建一个真正智能的、具有上下文理解能力的AI的**最后、也是最关键的一块基石**。

---

### 2.9. `IOrchestrationService` (智能编排服务)

`IOrchestrationService` 是我们整个 `Core` 模块的**大脑和中枢神经系统**。它位于我们所有底层服务（`ILLMService`, `IWorldDataService`, `IPromptFactoryService` 等）之上，作为所有复杂业务逻辑的**唯一入口和总指挥**。

#### 2.9.1. 角色与定位：高级的“大黑盒”

-   **定位：** `IOrchestrationService` 是一个高级的、有状态的服务。它的核心使命是将复杂的、多步骤的AI交互流程（如工具调用、指令执行）**完全封装**，为上游（如 `Governor` 门面）提供一个极其简单的、面向意图的接口。
-   **总消费者：** 它是我们架构中大多数底层服务的**最终消费者**。它会像一位指挥家一样，调度 `ILLMService`, `IPromptFactoryService`, `IWorldDataService`, `ICommandService`, `IHistoryService` 等所有服务协同工作，完成一个完整的目标。
-   **容器管理：** 其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。**

#### 2.9.2. 核心工作流：工具辅助查询 (Tool-Assisted Query)

`IOrchestrationService` 提供一个核心的“高级请求”方法：`ExecuteToolAssistedQueryAsync`。该方法严格遵循您定义的五步工作流，完整地实现了一次“Function Calling”交互。

**工作流程详解：**

1.  **【意图识别 -> AI决策】**
    -   接收到用户的原始查询（例如：“我们殖民地现在情况怎么样？”）。
    -   调用 `IPromptFactoryService` 将查询和可用工具列表组装成一个初始的 `Function Calling` 提示词。
    -   调用 `ILLMService.GetFunctionCallsAsync()`，将提示词发送给LLM，请求其决策。LLM返回它认为需要调用的函数和参数（例如：`get_colony_status`）。
    -   **分支判断：** 如果LLM认为无需调用任何工具，则直接进入步骤 4，进行一次简单的问答。

2.  **【决策执行 -> 安全数据获取/指令下达】**
    -   服务检查LLM返回的函数名。
    -   **“读”操作：** 如果是 `get_...` 类的查询函数，服务将调用 **`IWorldDataService`** 去安全地执行，并获取返回的数据（例如：一份包含资源、心情、威胁等级的殖民地状态报告）。
    -   **“写”操作：** 如果是 `spawn_...` 或 `set_...` 类的指令函数，服务将调用 **`ICommandService`** 去安全地执行，并获取操作结果。

3.  **【结果整合 -> 组建新提示词】**
    -   将上一步中工具/指令的执行结果（无论是数据报告还是“操作成功”的消息），再次调用 **`IPromptFactoryService`**。
    -   工厂服务会将这个“关键新信息”与用户的原始问题、对话历史等上下文组装成一个**全新的、信息更完备的**提示词。例如：“用户的原始问题是‘我们情况怎么样？’，我们刚刚执行了 `get_colony_status` 工具，返回的结果是[...详细报告...]. 现在，请你基于这些信息，为用户生成一段自然语言的总结。”

4.  **【最终响应 -> 流式返回】**
    -   将这个最终的、信息完备的提示词，通过 **`ILLMService.StreamResponseAsync()`** 发送给LLM。
    -   LLM此时不再需要思考或调用工具，它的唯一任务就是将输入的信息，转化为一段通顺、流畅、人性化的自然语言。
    -   `IOrchestrationService` 将从 `ILLMService` 获取到的**流式响应**，逐块地、实时地向上游返回。

5.  **【记录归档 (Archiving)】**
    -   在工作流的最后，`IOrchestrationService` 将严格遵守 `IHistoryService` 定义的**“最终结果唯一性”**原则。
    -   它将把用户的**初始问题**和自己生成的**最终自然语言回复**，作为两次独立的对话条目，调用 `IHistoryService.RecordEntryAsync` 进行持久化。

**结论：**
`IOrchestrationService`

---

### 2.10. `ICommandService` (指令安全下达服务)

`ICommandService` 是我们整个架构的“**执行手臂**”，是AI的智慧能够转化为对游戏世界实际影响的**唯一、强制性通道**。它与 `IWorldDataService`（安全读取）互为镜像，共同构成了“知行合一”的完整闭环。

#### 2.10.1. 角色与定位：AI的“首席执行官”

-   **定位：** 这是一个**高权限、高风险、高可靠性**的服务。它的存在，是为了将AI的“执行意图”安全、可控、可审计地转化为游戏内的实际行动。
-   **独占消费者：** **[核心安全原则]** `ICommandService` 绝不能被直接暴露给玩家或UI层，否则它就沦为了一个作弊器。它的**唯一合法调用者**，必须、也只能是 **`IOrchestrationService`**。只有“总指挥”才有权命令“首席执行官”去行动。
-   **容器管理：** 其实现类**必须在 `ServiceContainer` 中作为单例注册，并由容器统一管理其生命周期。**

#### 2.10.2. 核心职责：安全、验证、审计

1.  **绝对的线程安全 (Absolute Thread Safety):**
    -   这是其存在的首要理由。`ICommandService` 的内部实现，将会是 `ISchedulerService` 的**最大、最重要的客户**。
    -   **每一条**最终执行的、会修改游戏状态的指令（如 `GenSpawn.Spawn`），都**必须**通过 `ISchedulerService` 安全地调度到主线程执行。

2.  **指令验证与清理 (Command Validation & Sanitization):**
    -   作为防止AI“发疯”或被恶意提示利用的关键防线，`ICommandService` **必须**在执行任何指令前进行严格的合法性检查，包括但不限于：权限检查、资源检查、逻辑检查（如坐标有效性）。

3.  **可审计性 (Auditing):**
    -   由于所有“写”操作都经过这个唯一的瓶颈，我们可以在此记录详尽的**审计日志**，内容包括：时间、发起者(AI)、指令、参数和结果。

4.  **指令抽象 (Command Abstraction):**
    -   `ICommandService` 将底层的 RimWorld API 封装成一系列更高层、面向意图的、标准化的指令（如 `spawn_item`）。它负责将这些高级指令安全地翻译为具体的底层API调用。

#### 2.10.3. 接口与工作流程

**接口设计：**
```csharp
// 代表指令执行的结果
public class CommandResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } // 例如 "成功生成20份简易食物" 或 "错误：目标坐标无效"
}

// 指令服务的核心接口
public interface ICommandService
{
    // 一个统一的、异步的指令执行入口
    Task<CommandResult> ExecuteCommandAsync(string commandName, Dictionary<string, object> parameters);
}
```
**工作流程：**
`IOrchestrationService` 从LLM处获得一个执行指令的决策后，调用 `ICommandService.ExecuteCommandAsync()`。`ICommandService` 随即进行查找、验证、封装和安全的跨线程调度，最终将 `CommandResult` 返回给 `IOrchestrationService`。

**结论：**
`ICommandService` 的设计，为我们宏伟的架构补上了最后一块“执行”拼图。它确保了AI的所有“写”操作都在一个**安全、可靠、可控**的框架内进行，使我们的AI真正从一个“思想家”，蜕变成了一个能知能行的“行动者”。