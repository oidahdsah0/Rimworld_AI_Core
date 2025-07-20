# 👨‍💻 RimAI 开发者指南

*完整的开发流程、最佳实践和问题解决方案*

## 🚀 开发流程

### 1. 项目设置
```bash
# 克隆项目
git clone <repository-url>
cd Rimworld_AI_Core

# 还原NuGet包
dotnet restore RimAI.Core.sln

# 编译项目
dotnet build RimAI.Core.sln --configuration Debug
```

### 2. 开发环境配置
```xml
<!-- RimAI.Core.csproj 关键配置 -->
<TargetFramework>net48</TargetFramework>
<LangVersion>latest</LangVersion>
<OutputPath>Assemblies/</OutputPath>
```

### 3. 调试设置
- **启动项目**: 设置RimWorld.exe为启动程序
- **工作目录**: RimWorld安装目录
- **命令行参数**: `-dev -logverbose`

## 🧱 创建AI官员完整流程

### 步骤1: 定义官员类
```csharp
using RimAI.Core.Officers.Base;
using RimAI.Core.Architecture.Interfaces;

namespace RimAI.Core.Officers
{
    public class MedicalOfficer : OfficerBase
    {
        public override string Name => "医疗官";
        public override string Description => "专业医疗建议和健康管理";
        public override string IconPath => "UI/Icons/Medical";
        public override OfficerRole Role => OfficerRole.Medical;
        
        // 设置专业模板ID
        protected override string QuickAdviceTemplateId => "medical.quick";
        protected override string DetailedAdviceTemplateId => "medical.detailed";
    }
}
```

### 步骤2: 实现核心逻辑
```csharp
protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
{
    try
    {
        // 1. 获取医疗相关数据
        var colonistHealth = await GetColonistHealthDataAsync(cancellationToken);
        var medicalSupplies = await GetMedicalSuppliesAsync(cancellationToken);
        
        // 2. 构建专业上下文
        var context = await BuildContextAsync(cancellationToken);
        context["healthData"] = colonistHealth;
        context["supplies"] = medicalSupplies;
        context["medicalPriorities"] = AnalyzeMedicalPriorities(colonistHealth);
        
        // 3. 构建专业提示词
        var prompt = _promptBuilder.BuildPrompt(QuickAdviceTemplateId, context);
        if (string.IsNullOrEmpty(prompt))
        {
            prompt = BuildDefaultMedicalPrompt(context);
        }
        
        // 4. 调用AI并返回结果
        var options = CreateLLMOptions(temperature: 0.3f); // 医疗建议需要更保守
        return await _llmService.SendMessageAsync(prompt, options, cancellationToken);
    }
    catch (Exception ex)
    {
        Log.Error($"[MedicalOfficer] 医疗建议请求失败: {ex.Message}");
        return GetErrorResponse("医疗系统暂时不可用，请稍后重试");
    }
}

// 辅助方法
private async Task<Dictionary<string, object>> GetColonistHealthDataAsync(CancellationToken token)
{
    return await _cacheService.GetOrCreateAsync(
        "medical_health_data",
        async () => {
            // 实际的健康数据收集逻辑
            var healthData = new Dictionary<string, object>();
            // ... 收集殖民者健康状况
            return healthData;
        },
        TimeSpan.FromMinutes(1) // 健康数据缓存1分钟
    );
}
```

### 步骤3: 注册服务
```csharp
// 在ServiceContainer.RegisterDefaultServices()中添加
RegisterInstance<IAIOfficer>(MedicalOfficer.Instance);
RegisterInstance<MedicalOfficer>(MedicalOfficer.Instance);

// 或在CoreServices中添加访问器
public static MedicalOfficer MedicalOfficer => 
    ServiceContainer.Instance.GetService<MedicalOfficer>();
```

### 步骤4: UI集成
```csharp
// 在MainTabWindow_RimAI.cs中添加按钮
private void DrawMedicalButton(Rect rect)
{
    if (Widgets.ButtonText(rect, "🏥 医疗建议"))
    {
        ProcessMedicalRequest();
    }
}

private async void ProcessMedicalRequest()
{
    var medicalOfficer = CoreServices.MedicalOfficer;
    if (medicalOfficer?.IsAvailable == true)
    {
        var advice = await medicalOfficer.ProvideAdviceAsync();
        UpdateResponseText($"🏥 医疗官建议:\n\n{advice}");
    }
}
```

## 🎨 UI开发最佳实践

### 1. 异步UI处理
```csharp
// ❌ 错误 - 会阻塞UI线程
private void OnButtonClick()
{
    var result = aiService.GetAdvice().Result; // 危险！
    UpdateUI(result);
}

// ✅ 正确 - 异步处理
private async void OnButtonClick()
{
    try 
    {
        UpdateUI("正在处理...");
        var result = await aiService.GetAdviceAsync();
        UpdateUI(result);
    }
    catch (Exception ex)
    {
        UpdateUI($"处理失败: {ex.Message}");
    }
}
```

### 2. 响应式布局
```csharp
public override void DoWindowContents(Rect inRect)
{
    var listing = new Listing_Standard();
    listing.Begin(inRect);
    
    // 使用相对尺寸而非固定像素
    var buttonHeight = 35f;
    var spacing = 10f;
    
    if (listing.ButtonText("AI建议", buttonHeight))
    {
        ProcessAIRequest();
    }
    
    listing.Gap(spacing);
    
    // 动态文本区域
    var textRect = listing.GetRect(inRect.height - listing.CurHeight - 20f);
    Widgets.TextArea(textRect, responseText, true);
    
    listing.End();
}
```

### 3. 状态管理
```csharp
public class UIState
{
    public bool IsProcessing { get; set; }
    public string CurrentResponse { get; set; } = "";
    public DateTime LastUpdate { get; set; }
    
    public void SetProcessing(bool processing)
    {
        IsProcessing = processing;
        if (processing)
        {
            CurrentResponse = "正在处理中...";
            LastUpdate = DateTime.Now;
        }
    }
}
```

## 🔧 服务开发模式

### 1. 创建自定义服务
```csharp
// 定义接口
public interface IWeatherService
{
    Task<WeatherInfo> GetCurrentWeatherAsync();
    Task<WeatherForecast> GetForecastAsync(int days);
}

// 实现服务
public class WeatherService : IWeatherService
{
    private static WeatherService _instance;
    public static WeatherService Instance => _instance ??= new WeatherService();
    
    private readonly ICacheService _cache;
    
    private WeatherService()
    {
        _cache = CoreServices.CacheService;
    }
    
    public async Task<WeatherInfo> GetCurrentWeatherAsync()
    {
        return await _cache.GetOrCreateAsync(
            "current_weather",
            async () => await CollectWeatherDataAsync(),
            TimeSpan.FromMinutes(10)
        );
    }
}
```

### 2. 服务注册
```csharp
// 在ServiceContainer中注册
RegisterInstance<IWeatherService>(WeatherService.Instance);

// 在CoreServices中添加访问器
public static IWeatherService Weather => 
    ServiceContainer.Instance.GetService<IWeatherService>();
```

## 📊 数据分析集成

### 1. 使用ColonyAnalyzer
```csharp
public class ResourceAnalyzer
{
    private readonly IColonyAnalyzer _analyzer;
    
    public ResourceAnalyzer()
    {
        _analyzer = CoreServices.Analyzer;
    }
    
    public async Task<ResourceReport> AnalyzeResourcesAsync()
    {
        // 获取快速分析数据
        var quickAnalysis = await _analyzer.GetQuickAnalysisAsync();
        
        // 基于快速分析构建详细报告
        var report = new ResourceReport
        {
            OverallStatus = quickAnalysis.OverallRiskLevel,
            CriticalShortages = ExtractCriticalItems(quickAnalysis),
            // ... 更多分析逻辑
        };
        
        return report;
    }
}
```

### 2. 自定义分析器
```csharp
public class ThreatAnalyzer
{
    public async Task<List<ThreatInfo>> AnalyzeThreatsAsync()
    {
        var threats = new List<ThreatInfo>();
        
        // 分析当前威胁
        foreach (var incident in Find.World.worldObjects.Incidents)
        {
            var threat = new ThreatInfo
            {
                Type = incident.def.defName,
                Level = CalculateThreatLevel(incident),
                Description = incident.GetDescription(),
                DetectedAt = DateTime.Now
            };
            threats.Add(threat);
        }
        
        return threats;
    }
}
```

## 🎯 事件系统开发

### 1. 创建自定义事件
```csharp
using RimAI.Core.Architecture.Interfaces;

public class ResourceShortageEvent : IEvent
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; } = DateTime.Now;
    public string EventType => "ResourceShortage";
    
    public string ResourceType { get; set; }
    public float CurrentAmount { get; set; }
    public float RequiredAmount { get; set; }
    public ThreatLevel Severity { get; set; }
    
    public ResourceShortageEvent(string resourceType, float current, float required)
    {
        ResourceType = resourceType;
        CurrentAmount = current;
        RequiredAmount = required;
        Severity = CalculateSeverity();
    }
}
```

### 2. 创建事件监听器
```csharp
public class ResourceShortageListener : IEventHandler<ResourceShortageEvent>
{
    public async Task HandleAsync(ResourceShortageEvent eventArgs)
    {
        Log.Warning($"[ResourceMonitor] 资源短缺警告: {eventArgs.ResourceType}");
        
        // 自动触发补充建议
        if (eventArgs.Severity >= ThreatLevel.High)
        {
            var governor = CoreServices.Governor;
            await governor?.HandleUserQueryAsync($"如何解决{eventArgs.ResourceType}短缺问题？");
        }
        
        // 发送通知给UI
        await CoreServices.EventBus.PublishAsync(new UINotificationEvent(
            $"⚠️ {eventArgs.ResourceType}储量不足",
            NotificationType.Warning
        ));
    }
}
```

### 3. 事件发布和订阅
```csharp
// 注册监听器
var eventBus = CoreServices.EventBus;
eventBus.Subscribe<ResourceShortageEvent>(new ResourceShortageListener());

// 发布事件
await eventBus.PublishAsync(new ResourceShortageEvent("食物", 50f, 200f));
```

## 🧪 测试开发

### 1. 单元测试设置
```csharp
[TestClass]
public class GovernorTests
{
    private Governor _governor;
    private Mock<ILLMService> _mockLLMService;
    
    [TestInitialize]
    public void Setup()
    {
        _mockLLMService = new Mock<ILLMService>();
        _governor = new Governor();
        // 注入Mock服务
    }
    
    [TestMethod]
    public async Task HandleUserQuery_ValidQuery_ReturnsResponse()
    {
        // Arrange
        var query = "殖民地状况如何？";
        var expectedResponse = "殖民地运行良好";
        _mockLLMService.Setup(x => x.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                      .ReturnsAsync(expectedResponse);
        
        // Act
        var result = await _governor.HandleUserQueryAsync(query);
        
        // Assert
        Assert.AreEqual(expectedResponse, result);
    }
}
```

### 2. 集成测试
```csharp
[TestClass]
public class ServiceIntegrationTests
{
    [TestMethod]
    public void ServiceContainer_RegisterAndRetrieve_Success()
    {
        // Arrange
        var container = ServiceContainer.Instance;
        var testService = new TestService();
        
        // Act
        container.RegisterInstance<ITestService>(testService);
        var retrieved = container.GetService<ITestService>();
        
        // Assert
        Assert.IsNotNull(retrieved);
        Assert.AreSame(testService, retrieved);
    }
}
```

## 🔍 调试和故障排除

### 1. 常见问题诊断
```csharp
public class DiagnosticTool
{
    public static void RunDiagnostics()
    {
        Log.Message("=== RimAI 诊断开始 ===");
        
        // 检查服务状态
        CheckServiceStatus();
        
        // 检查缓存状态
        CheckCacheStatus();
        
        // 检查事件系统
        CheckEventSystem();
        
        Log.Message("=== RimAI 诊断完成 ===");
    }
    
    private static void CheckServiceStatus()
    {
        var services = new[]
        {
            ("Governor", CoreServices.Governor),
            ("EventBus", CoreServices.EventBus),
            ("Cache", CoreServices.CacheService),
            ("LLM", CoreServices.LLMService)
        };
        
        foreach (var (name, service) in services)
        {
            var status = service != null ? "✅" : "❌";
            Log.Message($"[诊断] {name}: {status}");
        }
    }
}
```

### 2. 性能监控
```csharp
public class PerformanceMonitor
{
    private static readonly Dictionary<string, List<long>> _timings = new();
    
    public static async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> func)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await func();
            return result;
        }
        finally
        {
            stopwatch.Stop();
            RecordTiming(operation, stopwatch.ElapsedMilliseconds);
        }
    }
    
    private static void RecordTiming(string operation, long milliseconds)
    {
        if (!_timings.ContainsKey(operation))
            _timings[operation] = new List<long>();
        
        _timings[operation].Add(milliseconds);
        
        if (_timings[operation].Count % 10 == 0)
        {
            var avg = _timings[operation].Average();
            Log.Message($"[性能] {operation} 平均耗时: {avg:F2}ms");
        }
    }
}
```

## 📝 代码规范

### 1. 命名约定
```csharp
// 类名: PascalCase
public class ResourceManager

// 方法名: PascalCase + Async后缀(如果是异步)
public async Task<string> GetResourceDataAsync()

// 属性名: PascalCase
public string ResourceName { get; set; }

// 私有字段: _camelCase
private readonly IService _service;

// 常量: UPPER_CASE
private const int MAX_RETRIES = 3;
```

### 2. 注释规范
```csharp
/// <summary>
/// 获取资源状态的异步方法
/// </summary>
/// <param name="resourceType">资源类型</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>资源状态信息</returns>
/// <exception cref="ArgumentNullException">当resourceType为null时抛出</exception>
public async Task<ResourceStatus> GetResourceStatusAsync(
    string resourceType, 
    CancellationToken cancellationToken = default)
{
    // 方法实现...
}
```

### 3. 错误处理规范
```csharp
public async Task<string> ProcessRequestAsync(string input)
{
    try
    {
        // 输入验证
        if (string.IsNullOrEmpty(input))
            throw new ArgumentException("输入不能为空", nameof(input));
        
        // 主要逻辑
        var result = await ProcessLogicAsync(input);
        return result;
    }
    catch (ArgumentException ex)
    {
        Log.Warning($"[ProcessRequest] 输入参数错误: {ex.Message}");
        throw; // 重新抛出验证错误
    }
    catch (Exception ex)
    {
        Log.Error($"[ProcessRequest] 处理请求时发生错误: {ex.Message}");
        return GetDefaultErrorResponse();
    }
}
```

## 🚀 部署和发布

### 1. 构建配置
```xml
<!-- Release配置 -->
<PropertyGroup Condition="'$(Configuration)'=='Release'">
    <Optimize>true</Optimize>
    <DebugType>pdbonly</DebugType>
    <DefineConstants>TRACE</DefineConstants>
</PropertyGroup>
```

### 2. 发布检查清单
- [ ] 所有单元测试通过
- [ ] 集成测试验证
- [ ] 性能测试通过
- [ ] 内存泄漏检查
- [ ] 异常处理覆盖
- [ ] 日志级别设置正确
- [ ] 文档更新完成

---
*👨‍💻 遵循这个开发指南，你将能够高效地开发出高质量的RimAI组件和功能！*
