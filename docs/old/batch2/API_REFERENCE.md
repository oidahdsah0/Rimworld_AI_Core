# 📚 RimAI API 参考手册

*所有公共接口、类型定义和方法签名的完整技术参考*

## 🏗️ 核心架构API

### ServiceContainer
中央依赖注入容器，管理所有服务的生命周期。

```csharp
public class ServiceContainer
{
    // 单例实例
    public static ServiceContainer Instance { get; }
    
    // 服务获取
    public T GetService<T>() where T : class
    
    // 服务注册状态
    public Dictionary<Type, object> GetRegisteredServices()
    
    // 初始化
    public static void Initialize()
}
```

### CoreServices
统一的服务访问门面，提供类型安全的服务获取。

```csharp
public static class CoreServices
{
    // AI服务
    public static Governor Governor { get; }
    public static ILLMService LLMService { get; }
    public static IColonyAnalyzer Analyzer { get; }
    
    // 新架构核心服务
    public static IHistoryService History { get; }
    public static IPromptFactoryService PromptFactory { get; }
    
    // 基础架构服务
    public static ICacheService CacheService { get; }
    public static IEventBus EventBus { get; }
    public static IPersistenceService PersistenceService { get; }
    public static ISafeAccessService SafeAccessService { get; }
    
    // 玩家身份标识
    public static string PlayerStableId { get; }      // 用于数据关联，永不改变
    public static string PlayerDisplayName { get; }  // 用于UI显示，用户可修改
    
    // 系统状态
    public static bool AreServicesReady()
    public static string GetServiceStatusReport()
}
```

## 🧠 智能体与工具API (Agent & Tools API)

*这部分API是在 `v2.1` 中引入的，用于支持AI驱动的工具调用功能。*

### IDispatcherService
所有AI工具调度策略的统一接口。

```csharp
public interface IDispatcherService
{
    /// <summary>
    /// 异步地根据用户输入，从工具列表中选择一个合适的工具。
    /// </summary>
    /// <param name="userInput">用户输入的自然语言。</param>
    /// <param name="tools">可供AI选择的工具定义列表。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>一个 DispatchResult 对象，包含AI的决策。</returns>
    Task<DispatchResult> DispatchAsync(string userInput, List<AITool> tools, CancellationToken cancellationToken = default);
}
```

### DispatchResult
`IDispatcherService` 返回的决策结果。

```csharp
public class DispatchResult
{
    public string ToolName { get; set; }  // AI选择的工具名称
    public Dictionary<string, object> Parameters { get; set; } // AI提取的参数
    public bool Success { get; } // 指示决策是否成功
}
```

### IToolRegistryService
管理工具定义、映射和执行逻辑的核心服务。

```csharp
public interface IToolRegistryService
{
    /// <summary>
    /// 获取所有可供AI使用的工具的定义列表。
    /// </summary>
    List<AITool> GetAvailableTools();

    /// <summary>
    /// 根据工具名称获取其执行所需的信息（服务类型和执行器）。
    /// </summary>
    ToolExecutionInfo GetToolExecutionInfo(string toolName);
}
```

### ToolExecutionInfo
包含执行一个工具所需的所有信息。

```csharp
public class ToolExecutionInfo
{
    // 该工具依赖的C#服务类型
    public Type ServiceType { get; set; } 
    
    // 一个封装了具体执行逻辑的委托
    public Func<object, Dictionary<string, object>, Task<string>> Executor { get; set; }
}
```

### AITool & AIFunction
用于定义工具的数据模型，与OpenAI的Function Calling格式兼容。

```csharp
public class AITool
{
    public string Type { get; set; } // 总是 "function"
    public AIFunction Function { get; set; }
}

public class AIFunction
{
    public string Name { get; set; } // 工具名称
    public string Description { get; set; } // 工具功能描述
    public AIParameterSchema Parameters { get; set; } // 工具参数定义
}
```

## 🧔 角色与分析API (Pawn & Analysis API) - 新增

### IPawnAnalyzer
用于分析单个角色（Pawn）的服务接口。

```csharp
public interface IPawnAnalyzer
{
    /// <summary>
    /// 异步地根据姓名获取一个角色的详细信息。
    /// </summary>
    Task<string> GetPawnDetailsAsync(string pawnName, CancellationToken cancellationToken = default);
}
```

## 🏗️ 对话历史服务API

### IHistoryService
管理多参与者对话历史的核心服务接口。

```csharp
public interface IHistoryService : IPersistable
{
    // 对话管理
    string StartOrGetConversation(List<string> participantIds);
    void AddEntry(string conversationId, ConversationEntry entry);
    
    // 历史检索
    HistoricalContext GetHistoricalContextFor(List<string> primaryParticipants, int limit = 10);
}
```

### ConversationEntry
单条对话记录的数据结构。

```csharp
public class ConversationEntry : IExposable
{
    public string ParticipantId { get; set; }      // 发言者唯一ID
    public string Role { get; set; }               // 角色标签 ("user", "assistant", "character")
    public string Content { get; set; }            // 发言内容
    public long GameTicksTimestamp { get; set; }   // 游戏内时间戳
    
    public void ExposeData()
}
```

### HistoricalContext
结构化的历史上下文数据，区分主线对话和附加参考对话。

```csharp
public class HistoricalContext
{
    // 主线历史：当前对话者之间的直接对话记录
    public List<ConversationEntry> PrimaryHistory { get; set; }
    
    // 附加历史：包含当前对话者但也有其他人在场的对话记录
    public List<ConversationEntry> AncillaryHistory { get; set; }
}
```

## 🏭 提示词工厂服务API

### IPromptFactoryService
智能组装结构化提示词的服务接口。

```csharp
public interface IPromptFactoryService
{
    // 核心方法
    Task<PromptPayload> BuildStructuredPromptAsync(PromptBuildConfig config);
}
```

### PromptBuildConfig
定义构建提示词所需的所有输入信息。

```csharp
public class PromptBuildConfig
{
    public List<string> CurrentParticipants { get; set; }  // 当前对话参与者
    public string SystemPrompt { get; set; }               // 系统提示词
    public SceneContext Scene { get; set; }                // 场景上下文
    public AncillaryData OtherData { get; set; }          // 其他附加数据
    public int HistoryLimit { get; set; } = 10;           // 历史记录上限
}
```

### PromptPayload
最终输出的LLM友好格式，与OpenAI API兼容。

```csharp
public class PromptPayload
{
    public List<ChatMessage> Messages { get; set; }
}
```

### ChatMessage
单条聊天消息格式。

```csharp
public class ChatMessage
{
    public string Role { get; set; }     // "system", "user", "assistant"
    public string Content { get; set; }  // 消息内容
    public string Name { get; set; }     // 可选，发言者标识
}
```

### SceneContext
描述对话发生时的具体环境。

```csharp
public class SceneContext
{
    public string Scenario { get; set; }         // 场景描述
    public string Time { get; set; }             // 时间信息
    public string Location { get; set; }         // 地点信息
    public List<string> Participants { get; set; } // 在场人员
    public string Situation { get; set; }        // 当前情况
}
```

### AncillaryData
其他附加游戏数据。

```csharp
public class AncillaryData
{
    public string Weather { get; set; }         // 天气信息
    public string ReferenceInfo { get; set; }   // 参考资料
}
```

## 🤖 AI官员API

### IAIOfficer
AI官员基础接口。

```csharp
public interface IAIOfficer
{
    // 基本属性
    string Name { get; }
    string Description { get; }
    string IconPath { get; }
    OfficerRole Role { get; }
    bool IsAvailable { get; }
    
    // 核心方法
    Task<string> ProvideAdviceAsync(CancellationToken cancellationToken = default);
    void CancelCurrentOperation();
    string GetStatus();
}
```

### OfficerBase
AI官员抽象基类。

```csharp
public abstract class OfficerBase : IAIOfficer
{
    // 抽象属性
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string IconPath { get; }
    public abstract OfficerRole Role { get; }
    
    // 虚拟属性
    public virtual bool IsAvailable { get; }
    
    // 核心方法
    public virtual Task<string> ProvideAdviceAsync(CancellationToken cancellationToken = default)
    protected abstract Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken);
    
    // 控制方法
    public virtual void CancelCurrentOperation()
    public virtual string GetStatus()
}
```

### Governor
总督AI官员，系统默认的主要AI决策者。

```csharp
public class Governor : OfficerBase
{
    // 基本属性
    public override string Name => "总督";
    public override string Description => "殖民地的首席AI决策官";
    public override OfficerRole Role => OfficerRole.Governor;
    public override string IconPath => "UI/Icons/Governor";
    
    // 专业方法
    public async Task<string> HandleUserQueryAsync(string userQuery, CancellationToken cancellationToken = default);
}
```

### OfficerRole
官员角色枚举。

```csharp
public enum OfficerRole
{
    Governor,    // 总督
    Military,    // 军事
    Medical,     // 医疗
    Logistics,   // 后勤
    Research,    // 科研
    Diplomat,    // 外交
    Security,    // 安全
    Economy      // 经济
}
```

## 🔍 分析服务API

### IColonyAnalyzer
殖民地分析服务接口。

```csharp
public interface IColonyAnalyzer
{
    // 分析方法
    Task<ColonyAnalysisResult> AnalyzeColonyAsync(CancellationToken cancellationToken = default);
    Task<string> GetQuickStatusSummaryAsync(CancellationToken cancellationToken = default);
    Task<T> GetSpecializedAnalysisAsync<T>(CancellationToken cancellationToken = default) where T : class;
}
```

### ColonyAnalysisResult
殖民地分析结果数据结构。

```csharp
public class ColonyAnalysisResult
{
    public int ColonistCount { get; set; }
    public string ColonistStatus { get; set; }
    public int FoodDaysRemaining { get; set; }
    public string ThreatLevel { get; set; }
    public string OverallRiskLevel { get; set; }
    public string QuickSummary { get; set; }
    public DateTime AnalysisTime { get; set; }
    public Dictionary<string, object> AdditionalData { get; set; }
}
```

## 🗄️ 缓存服务API

### ICacheService
智能缓存服务接口。

```csharp
public interface ICacheService
{
    // 缓存操作
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    void Remove(string key);
    void Clear();
    bool Contains(string key);
}
```

### CacheService
缓存服务实现。

```csharp
public class CacheService : ICacheService
{
    // 统计信息
    public CacheStats GetStats()
    
    // 配置属性
    public TimeSpan DefaultExpiration { get; }
    public int MaxEntries { get; }
}
```

### CacheStats
缓存统计信息。

```csharp
public class CacheStats
{
    public int TotalEntries { get; set; }
    public int ExpiredEntries { get; set; }
    public int ActiveEntries { get; set; }
    public long TotalAccessCount { get; set; }
    public int MaxEntries { get; set; }
    public TimeSpan DefaultExpiration { get; set; }
}
```

## 💾 持久化服务API

### IPersistenceService
管理随存档数据和全局Mod设置的综合服务。

```csharp
public interface IPersistenceService
{
    // 随存档数据管理
    void RegisterPersistable(IPersistable persistable);
    void UnregisterPersistable(IPersistable persistable);
    void ExposeAllRegisteredData();
    void Load();
    void Save();
    
    // 全局设置管理
    Task SaveGlobalSettingAsync<T>(string key, T setting);
    Task<T> LoadGlobalSettingAsync<T>(string key);
}
```

### IPersistable
表示可随存档持久化的对象。

```csharp
public interface IPersistable
{
    // 数据暴露方法，由Scribe系统调用
    void ExposeData();
}
```

## 📡 事件系统API

### IEventBus
事件总线服务接口。

```csharp
public interface IEventBus
{
    // 事件发布
    Task PublishAsync<TEvent>(TEvent eventArgs, CancellationToken cancellationToken = default) where TEvent : IEvent;
    
    // 事件订阅
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    
    // 统计信息
    int GetSubscriberCount<TEvent>() where TEvent : IEvent;
}
```

### IEvent
事件基础接口。

```csharp
public interface IEvent
{
    string Id { get; }
    DateTime Timestamp { get; }
    string EventType { get; }
}
```

### IEventHandler<TEvent>
事件处理器接口。

```csharp
public interface IEventHandler<TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent eventArgs);
}
```

## 🛡️ 安全访问服务API

### ISafeAccessService
提供对RimWorld API的并发安全访问。

```csharp
public interface ISafeAccessService
{
    // 集合安全访问
    List<Pawn> GetColonistsSafe(Map map, int maxRetries = 3);
    List<Pawn> GetPrisonersSafe(Map map, int maxRetries = 3);
    List<Pawn> GetAllPawnsSafe(Map map, int maxRetries = 3);
    List<Building> GetBuildingsSafe(Map map, int maxRetries = 3);
    List<Thing> GetThingsSafe(Map map, ThingDef thingDef, int maxRetries = 3);
    List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group, int maxRetries = 3);
    
    // 单值安全访问
    int GetColonistCountSafe(Map map, int maxRetries = 3);
    WeatherDef GetCurrentWeatherSafe(Map map, int maxRetries = 3);
    Season GetCurrentSeasonSafe(Map map, int maxRetries = 3);
    int GetTicksGameSafe(int maxRetries = 3);
    
    // 统计和监控
    Dictionary<string, int> GetFailureStats();
    string GetStatusReport();
    void ClearStats();
}
```

## 🤖 LLM服务API

### ILLMService
AI模型调用服务接口。

```csharp
public interface ILLMService
{
    // 基本属性
    bool IsStreamingAvailable { get; }
    bool IsInitialized { get; }
    
    // 消息发送
    Task<string> SendMessageAsync(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default);
    Task<T> SendJsonRequestAsync<T>(string prompt, LLMRequestOptions options = null, CancellationToken cancellationToken = default) where T : class;
    Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMRequestOptions options = null, CancellationToken cancellationToken = default);
}
```

### LLMRequestOptions
LLM请求选项配置（来自Framework）。

```csharp
public class LLMRequestOptions
{
    public float Temperature { get; set; }
    public int MaxTokens { get; set; }
    public string Model { get; set; }
    public bool Stream { get; set; }
    public TimeSpan Timeout { get; set; }
    public Dictionary<string, object> AdditionalParameters { get; set; }
}
```

## 📝 提示词构建API（旧版，即将废弃）

### IPromptBuilder
传统的提示词构建服务接口。

```csharp
public interface IPromptBuilder
{
    // 模板构建
    string BuildPrompt(string templateId, Dictionary<string, object> context);
    
    // 模板管理
    void RegisterTemplate(string id, PromptTemplate template);
    PromptTemplate GetTemplate(string id);
    bool TemplateExists(string id);
}
```

### PromptTemplate
提示词模板数据结构。

```csharp
public class PromptTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Template { get; set; }
    public PromptConstraints Constraints { get; set; }
    public Dictionary<string, string> Variables { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }
}
```

### PromptConstraints
提示词约束配置。

```csharp
public class PromptConstraints
{
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
    public List<string> SafetyRules { get; set; }
    public string ResponseFormat { get; set; }
    public bool RequireStreaming { get; set; }
    public TimeSpan? Timeout { get; set; }
}
```

## 🎨 UI组件API

### MainTabWindow_RimAI
主要UI窗口类。

```csharp
public class MainTabWindow_RimAI : MainTabWindow
{
    // 窗口属性
    public override Vector2 RequestedTabSize { get; }
    
    // 核心方法
    public override void DoWindowContents(Rect inRect);
    public override void PreOpen();
    
    // 私有状态
    private List<ChatMessage> _displayMessages;
    private Vector2 _scrollPosition;
    private string _currentInput;
    private bool _isProcessing;
    private string _conversationId;
}
```

### Dialog_OfficerSettings
官员设置对话框。

```csharp
public class Dialog_OfficerSettings : Window
{
    // 窗口属性
    public override Vector2 InitialSize { get; }
    
    // 核心方法
    public override void DoWindowContents(Rect inRect);
    
    // 构造函数
    public Dialog_OfficerSettings(IAIOfficer officer);
}
```

## 📊 数据模型和枚举

### ThreatLevel
威胁等级枚举。

```csharp
public enum ThreatLevel
{
    None,      // 无威胁
    Low,       // 低威胁
    Medium,    // 中等威胁
    High,      // 高威胁
    Critical   // 危急威胁
}
```

### ResourcePriority
资源优先级枚举。

```csharp
public enum ResourcePriority
{
    Low,
    Normal,
    High,
    Critical
}
```

### ColonyStatus
殖民地状态数据。

```csharp
public class ColonyStatus
{
    public int ColonistCount { get; set; }
    public string ResourceSummary { get; set; }
    public ThreatLevel ThreatLevel { get; set; }
    public List<string> ActiveEvents { get; set; }
    public string WeatherCondition { get; set; }
    public string Season { get; set; }
    public Dictionary<string, float> ResourceLevels { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

### ThreatInfo
威胁信息数据。

```csharp
public class ThreatInfo
{
    public string Type { get; set; }
    public ThreatLevel Level { get; set; }
    public string Description { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Details { get; set; }
}
```

## ⚙️ 设置和配置API

### CoreSettings
核心设置数据结构。

```csharp
public class CoreSettings : ModSettings
{
    public PlayerSettings Player { get; set; }
    public Dictionary<string, OfficerConfig> OfficerConfigs { get; set; }
    public Dictionary<string, PromptTemplate> CustomPrompts { get; set; }
    public UISettings UI { get; set; }
    public PerformanceSettings Performance { get; set; }
    public CacheSettings Cache { get; set; }
    public EventSettings Events { get; set; }
    public DebugSettings Debug { get; set; }
    
    public override void ExposeData();
}
```

### PlayerSettings
玩家设置。

```csharp
public class PlayerSettings
{
    public string Nickname { get; set; } = "指挥官";
}
```

### OfficerConfig
官员配置。

```csharp
public class OfficerConfig
{
    public bool Enabled { get; set; } = true;
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 1000;
    public Dictionary<string, object> CustomSettings { get; set; }
}
```

### SettingsManager
设置管理器。

```csharp
public static class SettingsManager
{
    public static CoreSettings Settings { get; }
    
    public static void SetSettings(CoreSettings settings);
    public static void SaveSettings();
    public static OfficerConfig GetOfficerConfig(string officerName);
    public static void ApplySettings();
}
```

## 🔧 系统组件API

### RimAICoreGameComponent
游戏组件，负责生命周期管理。

```csharp
public class RimAICoreGameComponent : GameComponent
{
    // 构造函数
    public RimAICoreGameComponent(Game game);
    
    // 生命周期方法
    public override void LoadedGame();
    public override void ExposeData();
    public override void GameComponentTick();
}
```

### RimAICoreMod
模组主类。

```csharp
public class RimAICoreMod : Mod
{
    // 构造函数
    public RimAICoreMod(ModContentPack content);
    
    // 设置窗口
    public override void DoSettingsWindowContents(Rect inRect);
    
    // 设置名称
    public override string SettingsCategory();
}
```

## 📋 服务状态和监控API

### 服务就绪检查
```csharp
// 检查所有核心服务是否就绪
bool isReady = CoreServices.AreServicesReady();

// 获取详细的服务状态报告
string report = CoreServices.GetServiceStatusReport();
```

### 性能监控
```csharp
// 缓存服务统计
var cacheStats = CoreServices.CacheService.GetStats();

// 安全访问服务统计
var safeAccessStats = CoreServices.SafeAccessService.GetFailureStats();
string safeAccessReport = CoreServices.SafeAccessService.GetStatusReport();

// 事件总线统计
int subscriberCount = CoreServices.EventBus.GetSubscriberCount<CustomEvent>();
```

---

*📚 本API参考手册提供了RimAI框架所有公共接口的完整技术规格。所有方法签名、参数类型和返回值都经过验证，确保准确性。*
