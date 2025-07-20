# 📚 RimAI API 参考手册

*完整的API接口、类型定义和使用示例*

## 🏗️ 核心架构API

### ServiceContainer
中央依赖注入容器，管理所有服务的生命周期

```csharp
public class ServiceContainer
{
    // 获取单例实例
    public static ServiceContainer Instance { get; }
    
    // 注册服务实例
    public void RegisterInstance<T>(T instance) where T : class
    
    // 注册服务工厂
    public void RegisterFactory<T>(Func<T> factory) where T : class
    
    // 获取服务
    public T GetService<T>() where T : class
    
    // 检查服务状态
    public string GetStatusInfo()
}
```

**使用示例**:
```csharp
// 注册服务
ServiceContainer.Instance.RegisterInstance<IMyService>(myServiceInstance);

// 获取服务
var myService = ServiceContainer.Instance.GetService<IMyService>();
```

### CoreServices
统一的服务访问门面，提供类型安全的服务获取

```csharp
public static class CoreServices
{
    // 核心AI服务
    public static Governor Governor { get; }
    public static IColonyAnalyzer Analyzer { get; }
    public static ILLMService LLMService { get; }
    
    // 基础服务
    public static ICacheService CacheService { get; }
    public static IEventBus EventBus { get; }
    public static IPromptBuilder PromptBuilder { get; }
    
    // RimWorld API 安全访问服务
    public static class SafeAccess
    {
        // 集合安全访问
        public static List<Pawn> GetColonistsSafe(Map map);
        public static List<Pawn> GetPrisonersSafe(Map map);
        public static List<Pawn> GetAllPawnsSafe(Map map);
        public static List<Building> GetBuildingsSafe(Map map);
        public static List<Thing> GetThingsSafe(Map map, ThingDef thingDef);
        public static List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group);
        
        // 单个对象安全访问
        public static int GetColonistCountSafe(Map map);
        public static WeatherDef GetCurrentWeatherSafe(Map map);
        public static Season GetCurrentSeasonSafe(Map map);
        public static int GetTicksGameSafe();
        
        // 统计监控
        public static string GetStatusReport();
    }
    
    // 状态检查
    public static bool AreServicesReady();
    public static string GetServiceStatusReport();
}
```

**使用示例**:
```csharp
// 推荐的服务获取方式
var governor = CoreServices.Governor;
var cache = CoreServices.CacheService;

// 安全访问RimWorld API - 自动处理并发异常
var colonists = CoreServices.SafeAccess.GetColonistsSafe(map);
var weather = CoreServices.SafeAccess.GetCurrentWeatherSafe(map);

// 检查服务状态
if (CoreServices.AreServicesReady())
{
    // 安全使用服务
}
```

## 🛡️ SafeAccessService API

### 核心功能
SafeAccessService 提供对 RimWorld API 的并发安全访问，自动处理 `InvalidOperationException` 和空引用异常。

```csharp
public static class SafeAccessService
{
    // 集合安全访问方法
    public static List<Pawn> GetColonistsSafe(Map map, int maxRetries = 3);
    public static List<Pawn> GetPrisonersSafe(Map map, int maxRetries = 3);
    public static List<Pawn> GetAllPawnsSafe(Map map, int maxRetries = 3);
    public static List<Building> GetBuildingsSafe(Map map, int maxRetries = 3);
    public static List<Thing> GetThingsSafe(Map map, ThingDef thingDef, int maxRetries = 3);
    public static List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group, int maxRetries = 3);
    
    // 单个对象安全访问方法
    public static int GetColonistCountSafe(Map map, int maxRetries = 3);
    public static WeatherDef GetCurrentWeatherSafe(Map map, int maxRetries = 3);
    public static Season GetCurrentSeasonSafe(Map map, int maxRetries = 3);
    public static int GetTicksGameSafe(int maxRetries = 3);
    
    // 批量操作安全包装器
    public static TResult SafePawnOperation<TResult>(
        List<Pawn> pawns,
        Func<List<Pawn>, TResult> operation,
        TResult fallbackValue,
        string operationName);
        
    public static TResult SafeBuildingOperation<TResult>(
        List<Building> buildings,
        Func<List<Building>, TResult> operation,
        TResult fallbackValue,
        string operationName);
        
    public static TResult SafeThingOperation<TResult>(
        List<Thing> things,
        Func<List<Thing>, TResult> operation,
        TResult fallbackValue,
        string operationName);
    
    // 统计和监控
    public static Dictionary<string, int> GetFailureStats();
    public static string GetStatusReport();
    public static void ClearStats();
}
```

**使用示例**:
```csharp
// 基础集合访问 - 自动重试和异常处理
var colonists = SafeAccessService.GetColonistsSafe(map);
var buildings = SafeAccessService.GetBuildingsSafe(map);
var food = SafeAccessService.GetThingGroupSafe(map, ThingRequestGroup.FoodSourceNotPlantOrTree);

// 安全操作包装器 - 防止操作中的异常
var healthyCount = SafeAccessService.SafePawnOperation(
    colonists,
    pawns => pawns.Count(p => !p.Downed && p.health.summaryHealth.SummaryHealthPercent > 0.8f),
    0,
    "CountHealthyColonists"
);

// 监控和统计
Log.Message(SafeAccessService.GetStatusReport());
```

## 🤖 AI官员API

### IAIOfficer
AI官员基础接口

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
AI官员抽象基类，提供通用功能实现

```csharp
public abstract class OfficerBase : IAIOfficer
{
    // 抽象属性 - 子类必须实现
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string IconPath { get; }
    public abstract OfficerRole Role { get; }
    
    // 虚拟属性 - 子类可以重写
    protected virtual string QuickAdviceTemplateId { get; }
    protected virtual string DetailedAdviceTemplateId { get; }
    
    // 核心方法
    public virtual Task<string> ProvideAdviceAsync(CancellationToken cancellationToken = default)
    protected abstract Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
    
    // 辅助方法
    protected virtual async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken)
    protected virtual string GenerateCacheKey(string operation)
    protected virtual LLMOptions CreateLLMOptions(float temperature = 0.7f)
}
```

**继承示例**:
```csharp
public class MedicalOfficer : OfficerBase
{
    public override string Name => "医疗官";
    public override string Description => "专业医疗建议和健康管理";
    public override OfficerRole Role => OfficerRole.Medical;
    public override string IconPath => "UI/Icons/Medical";
    
    protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
    {
        var context = await BuildContextAsync(cancellationToken);
        // 添加医疗专业数据
        context["healthData"] = await GetHealthDataAsync(cancellationToken);
        
        var prompt = _promptBuilder.BuildPrompt("medical.advice", context);
        return await _llmService.SendMessageAsync(prompt, CreateLLMOptions(0.3f), cancellationToken);
    }
}
```

### Governor
总督AI官员，系统默认的主要AI官员

```csharp
public class Governor : OfficerBase
{
    // 单例访问
    public static Governor Instance { get; }
    
    // 基本属性
    public override string Name => "总督";
    public override OfficerRole Role => OfficerRole.Governor;
    
    // 专业方法
    public async Task<string> HandleUserQueryAsync(string userQuery, CancellationToken cancellationToken = default)
    public async Task<string> GetColonyOverviewAsync(CancellationToken cancellationToken = default)
}
```

**使用示例**:
```csharp
// 通过CoreServices获取（推荐）
var governor = CoreServices.Governor;

// 用户查询处理
var response = await governor.HandleUserQueryAsync("当前殖民地状况如何？");

// 获取殖民地概览
var overview = await governor.GetColonyOverviewAsync();
```

## 📊 分析服务API

### IColonyAnalyzer
殖民地分析服务接口

```csharp
public interface IColonyAnalyzer
{
    // 快速分析
    Task<QuickAnalysisResult> GetQuickAnalysisAsync(CancellationToken cancellationToken = default);
    
    // 详细分析
    Task<DetailedAnalysisResult> GetDetailedAnalysisAsync(CancellationToken cancellationToken = default);
    
    // 威胁分析
    Task<List<ThreatInfo>> GetThreatsAsync(CancellationToken cancellationToken = default);
    
    // 资源分析
    Task<ResourceReport> GetResourceReportAsync(CancellationToken cancellationToken = default);
    
    // 状态检查
    bool IsAvailable { get; }
    string GetStatus();
}
```

### QuickAnalysisResult
快速分析结果数据结构

```csharp
public class QuickAnalysisResult
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

**使用示例**:
```csharp
var analyzer = CoreServices.Analyzer;
if (analyzer.IsAvailable)
{
    var analysis = await analyzer.GetQuickAnalysisAsync();
    Log.Message($"殖民地状态: {analysis.OverallRiskLevel}");
    Log.Message($"食物储备: {analysis.FoodDaysRemaining}天");
}
```

## 🗄️ 缓存服务API

### ICacheService
智能缓存服务接口

```csharp
public interface ICacheService
{
    // 获取或创建缓存项
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    
    // 缓存操作
    void Remove(string key);
    void Clear();
    bool Contains(string key);
}
```

### CacheService
缓存服务具体实现

```csharp
public class CacheService : ICacheService
{
    // 单例访问
    public static CacheService Instance { get; }
    
    // 统计信息
    public CacheStats GetStats()
    
    // 配置属性
    public TimeSpan DefaultExpiration { get; } // 5分钟
    public int MaxEntries { get; } // 1000项
}
```

### CacheStats
缓存统计信息

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

**使用示例**:
```csharp
var cache = CoreServices.CacheService;

// 基本缓存使用
var expensiveData = await cache.GetOrCreateAsync(
    "expensive_operation",
    async () => await PerformExpensiveOperation(),
    TimeSpan.FromMinutes(10)
);

// 检查缓存统计
if (cache is CacheService concreteCache)
{
    var stats = concreteCache.GetStats();
    Log.Message($"缓存命中率: {stats.ActiveEntries}/{stats.TotalEntries}");
}
```

## 📡 事件系统API

### IEventBus
事件总线服务接口

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
事件基础接口

```csharp
public interface IEvent
{
    string Id { get; }
    DateTime Timestamp { get; }
    string EventType { get; }
}
```

### IEventHandler<TEvent>
事件处理器接口

```csharp
public interface IEventHandler<TEvent> where TEvent : IEvent
{
    Task HandleAsync(TEvent eventArgs);
}
```

**使用示例**:
```csharp
// 定义自定义事件
public class CustomEvent : IEvent
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; } = DateTime.Now;
    public string EventType => "Custom";
    
    public string Message { get; set; }
}

// 创建事件处理器
public class CustomEventHandler : IEventHandler<CustomEvent>
{
    public async Task HandleAsync(CustomEvent eventArgs)
    {
        Log.Message($"处理自定义事件: {eventArgs.Message}");
        // 处理逻辑...
    }
}

// 注册监听器
var eventBus = CoreServices.EventBus;
eventBus.Subscribe<CustomEvent>(new CustomEventHandler());

// 发布事件
await eventBus.PublishAsync(new CustomEvent { Message = "测试消息" });
```

## 🤖 LLM服务API

### ILLMService
AI模型调用服务接口

```csharp
public interface ILLMService
{
    // 基本消息发送
    Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default);
    
    // 带选项的消息发送
    Task<string> SendMessageAsync(string message, LLMOptions options, CancellationToken cancellationToken = default);
    
    // 流式响应
    IAsyncEnumerable<string> SendMessageStreamAsync(string message, LLMOptions options, CancellationToken cancellationToken = default);
    
    // 状态检查
    bool IsAvailable { get; }
    string GetStatus();
}
```

### LLMOptions
LLM调用选项配置

```csharp
public class LLMOptions
{
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 1000;
    public string Model { get; set; }
    public bool Stream { get; set; } = false;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    public Dictionary<string, object> AdditionalParameters { get; set; } = new();
}
```

**使用示例**:
```csharp
var llmService = CoreServices.LLMService;

// 基本调用
var response = await llmService.SendMessageAsync("你好，AI助手！");

// 带选项调用
var options = new LLMOptions
{
    Temperature = 0.3f,
    MaxTokens = 500,
    Model = "gpt-4"
};
var detailedResponse = await llmService.SendMessageAsync("详细分析殖民地状况", options);

// 流式调用
await foreach (var chunk in llmService.SendMessageStreamAsync("长篇分析", options))
{
    Console.Write(chunk);
}
```

## 📝 提示词API

### IPromptBuilder
提示词构建服务接口

```csharp
public interface IPromptBuilder
{
    // 构建提示词
    string BuildPrompt(string templateId, Dictionary<string, object> context);
    
    // 模板管理
    void RegisterTemplate(string templateId, string template);
    bool HasTemplate(string templateId);
    void RemoveTemplate(string templateId);
    
    // 变量处理
    string ProcessVariables(string template, Dictionary<string, object> variables);
}
```

**使用示例**:
```csharp
var promptBuilder = CoreServices.PromptBuilder;

// 注册模板
promptBuilder.RegisterTemplate("medical.advice", 
    "作为医疗官，基于以下数据提供建议：\n健康状况：{healthData}\n医疗用品：{supplies}");

// 使用模板
var context = new Dictionary<string, object>
{
    ["healthData"] = "3名殖民者受伤",
    ["supplies"] = "药品充足"
};

var prompt = promptBuilder.BuildPrompt("medical.advice", context);
```

## 🎨 UI组件API

### MainTabWindow_RimAI
主要UI窗口类

```csharp
public class MainTabWindow_RimAI : MainTabWindow
{
    // 窗口属性
    public override Vector2 RequestedTabSize => new Vector2(400f, 600f);
    
    // 主要方法
    public override void DoWindowContents(Rect inRect);
    
    // UI状态
    private string responseText = "";
    private bool isProcessing = false;
}
```

### UI工具方法
```csharp
// 异步按钮处理
private async void ProcessGovernorRequest()
{
    try
    {
        isProcessing = true;
        responseText = "正在咨询总督...";
        
        var governor = CoreServices.Governor;
        var advice = await governor.ProvideAdviceAsync();
        
        responseText = $"🏛️ 总督建议:\n\n{advice}";
    }
    catch (Exception ex)
    {
        responseText = $"❌ 错误: {ex.Message}";
    }
    finally
    {
        isProcessing = false;
    }
}
```

## 📊 数据模型API

### 核心枚举类型
```csharp
// 官员角色
public enum OfficerRole
{
    Governor,        // 总督
    Military,        // 军事  
    Medical,         // 医疗
    Logistics,       // 后勤
    Research,        // 科研
    Diplomat,        // 外交
    Security,        // 安全
    Economy          // 经济
}

// 威胁等级
public enum ThreatLevel
{
    None,      // 无威胁
    Low,       // 低威胁  
    Medium,    // 中等威胁
    High,      // 高威胁
    Critical   // 危急威胁
}

// 资源优先级
public enum ResourcePriority
{
    Low,
    Normal,
    High,
    Critical
}
```

### 数据结构

#### ColonyStatus
殖民地状态数据

```csharp
public class ColonyStatus
{
    public int ColonistCount { get; set; }
    public string ResourceSummary { get; set; }
    public ThreatLevel ThreatLevel { get; set; }
    public List<string> ActiveEvents { get; set; } = new List<string>();
    public string WeatherCondition { get; set; }
    public string Season { get; set; }
    public Dictionary<string, float> ResourceLevels { get; set; } = new Dictionary<string, float>();
    public List<ColonistInfo> Colonists { get; set; } = new List<ColonistInfo>();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
```

#### ThreatInfo
威胁信息

```csharp
public class ThreatInfo
{
    public string Type { get; set; }
    public ThreatLevel Level { get; set; }
    public string Description { get; set; }
    public DateTime DetectedAt { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
}
```

#### ResourceReport
资源报告

```csharp
public class ResourceReport
{
    public Dictionary<string, ResourceStatus> Resources { get; set; } = new Dictionary<string, ResourceStatus>();
    public List<string> CriticalShortages { get; set; } = new List<string>();
    public List<string> Surpluses { get; set; } = new List<string>();
    public string OverallStatus { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}
```

## 🔧 扩展点API

### 服务扩展
```csharp
// 创建自定义服务接口
public interface ICustomService
{
    Task<string> DoSomethingAsync();
}

// 实现服务
public class CustomService : ICustomService
{
    public async Task<string> DoSomethingAsync()
    {
        // 实现逻辑
        return "完成";
    }
}

// 注册服务
ServiceContainer.Instance.RegisterInstance<ICustomService>(new CustomService());

// 在CoreServices中添加访问器
public static ICustomService Custom => ServiceContainer.Instance.GetService<ICustomService>();
```

### 官员扩展
```csharp
// 扩展新的官员角色
public class EconomyOfficer : OfficerBase
{
    public override string Name => "经济官";
    public override OfficerRole Role => OfficerRole.Economy;
    
    protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
    {
        // 经济专业分析逻辑
        var economicData = await GetEconomicDataAsync(cancellationToken);
        var context = await BuildContextAsync(cancellationToken);
        context["economicData"] = economicData;
        
        var prompt = _promptBuilder.BuildPrompt("economy.advice", context);
        return await _llmService.SendMessageAsync(prompt, CreateLLMOptions(0.5f), cancellationToken);
    }
}
```

## 📋 API使用最佳实践

### 1. 异步编程
```csharp
// ✅ 正确的异步调用
public async Task<string> GetAnalysisAsync()
{
    var analyzer = CoreServices.Analyzer;
    var result = await analyzer.GetQuickAnalysisAsync();
    return result.QuickSummary;
}

// ❌ 错误的阻塞调用
public string GetAnalysis()
{
    var result = CoreServices.Analyzer.GetQuickAnalysisAsync().Result; // 会阻塞
    return result.QuickSummary;
}
```

### 2. 错误处理
```csharp
public async Task<string> SafeAPICall()
{
    try
    {
        var service = CoreServices.Governor;
        if (service?.IsAvailable != true)
        {
            return "服务不可用";
        }
        
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        return await service.ProvideAdviceAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        return "操作已取消";
    }
    catch (Exception ex)
    {
        Log.Error($"API调用失败: {ex.Message}");
        return "系统错误，请稍后重试";
    }
}
```

### 3. 资源管理
```csharp
public async Task<string> ProperResourceManagement()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    
    try
    {
        var tasks = new[]
        {
            CoreServices.Analyzer.GetQuickAnalysisAsync(cts.Token),
            CoreServices.Governor.ProvideAdviceAsync(cts.Token)
        };
        
        await Task.WhenAll(tasks);
        
        return "操作完成";
    }
    finally
    {
        // 确保资源被正确清理
        cts?.Dispose();
    }
}
```

---
*📚 这个API参考手册涵盖了RimAI框架的所有核心接口和使用方法，是开发过程中的重要参考资料！*
