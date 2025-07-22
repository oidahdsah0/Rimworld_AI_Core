# 🚀 RimAI 快速入门指南

*5分钟快速上手RimAI框架开发*

## 📋 开发环境准备

### 必需工具
- **Visual Studio 2022** 或 **VS Code**
- **.NET Framework 4.8** 
- **RimWorld 1.5+** (开发测试)
- **Git** (版本控制)

### 项目结构一览
```
RimAI.Core/
├── Source/
│   ├── Architecture/     # 🏗️ 核心架构
│   ├── Officers/        # 👨‍💼 AI官员
│   ├── Services/        # 🛠️ 核心服务
│   ├── Analysis/        # 📊 分析组件
│   └── UI/              # 🖥️ 用户界面
└── Assemblies/          # 📦 编译输出
```

## 🎯 3步开始开发

### 第1步: 获取服务实例
```csharp
// ✅ 正确方式 - 使用企业级依赖注入
var governor = CoreServices.Governor;
var analyzer = CoreServices.Analyzer;
var cache = CoreServices.CacheService;

// ❌ 错误方式 - 直接单例调用
// var governor = Governor.Instance; // 不要这样做！
```

### 第2步: 创建你的第一个AI官员
```csharp
using RimAI.Core.Officers.Base;
using RimAI.Core.Architecture.Interfaces;

public class MyCustomOfficer : OfficerBase
{
    public override string Name => "我的官员";
    public override string Description => "自定义AI官员";
    public override OfficerRole Role => OfficerRole.Governor;
    public override string IconPath => "UI/Icons/MyOfficer";

    protected override async Task<string> ExecuteAdviceRequest(CancellationToken cancellationToken)
    {
        // 获取殖民地数据
        var context = await BuildContextAsync(cancellationToken);
        
        // 构建提示词并调用AI
        var prompt = _promptBuilder.BuildPrompt("my_officer.advice", context);
        return await _llmService.SendMessageAsync(prompt, cancellationToken);
    }
}
```

### 第3步: 注册和使用
```csharp
// 在ServiceContainer中注册
ServiceContainer.Instance.RegisterInstance<MyCustomOfficer>(new MyCustomOfficer());

// 在UI中使用
var myOfficer = CoreServices.GetService<MyCustomOfficer>();
var advice = await myOfficer.ProvideAdviceAsync();
```

## 🛠️ 核心服务速览

| 服务 | 用途 | 获取方式 |
|------|------|----------|
| **Governor** | AI总督官员 | `CoreServices.Governor` |
| **Analyzer** | 殖民地分析 | `CoreServices.Analyzer` |
| **EventBus** | 事件通信 | `CoreServices.EventBus` |
| **CacheService** | 智能缓存 | `CoreServices.CacheService` |
| **LLMService** | AI模型调用 | `CoreServices.LLMService` |
| **SafeAccess** | RimWorld API安全访问 | `CoreServices.SafeAccess` |
| **PersistenceService** | 持久化存储 | `CoreServices.PersistenceService` |

## 📝 常用代码模式

### RimWorld数据安全访问
```csharp
// ✅ 安全获取殖民者列表
var colonists = await CoreServices.SafeAccess.GetColonistsSafeAsync(map);

// ✅ 安全获取资源数据
var food = await CoreServices.SafeAccess.GetResourcesSafeAsync(map, "食物");

// ✅ 安全处理Pawn集合
await CoreServices.SafeAccess.SafePawnOperationAsync(colonists, async pawn =>
{
    var health = pawn.health.summaryHealth.SummaryHealthPercent;
    await ProcessPawnAsync(pawn, health);
});
```

### 异步AI调用
```csharp
var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
```

### 缓存数据
```csharp
var cachedData = await _cacheService.GetOrCreateAsync(
    "my_key", 
    async () => await ExpensiveOperation(), 
    TimeSpan.FromMinutes(5)
);
```

### 发布事件
```csharp
await CoreServices.EventBus.PublishAsync(new MyCustomEvent(data));
```

### 持久化数据
```csharp
// 1. 随存档数据 (详情见开发者指南)
public class MySaveableComponent : IPersistable
{
    public void ExposeData() { /* ... Scribe code ... */ }
    public MySaveableComponent() { CoreServices.PersistenceService.RegisterPersistable(this); }
}

// 2. 全局设置
var settings = new { MySetting = "value" };
await CoreServices.PersistenceService.SaveGlobalSettingAsync("MySettings", settings);
var loaded = await CoreServices.PersistenceService.LoadGlobalSettingAsync<object>("MySettings");
```

### 分析殖民地
```csharp
var analysis = await CoreServices.Analyzer.GetQuickAnalysisAsync();
Log.Message($"殖民地状态: {analysis.OverallStatus}");
```

## 🎨 UI集成

### 添加按钮到主界面
```csharp
// 在MainTabWindow_RimAI.cs中添加
if (Widgets.ButtonText(buttonRect, "我的功能"))
{
    var myOfficer = CoreServices.GetService<MyCustomOfficer>();
    ProcessCustomRequest(myOfficer);
}
```

## 🔧 调试技巧

### 日志输出
```csharp
Log.Message("[MyMod] 信息日志");
Log.Warning("[MyMod] 警告日志");  
Log.Error("[MyMod] 错误日志");
```

### 服务状态检查
```csharp
if (!CoreServices.AreServicesReady())
{
    Log.Error("核心服务未就绪！");
    return;
}
```

## 🚨 常见错误避免

### ❌ 直接访问RimWorld集合
```csharp
// 不要这样做 - 可能引发InvalidOperationException
var colonists = map.mapPawns.FreeColonists; // 并发修改异常风险
var items = map.listerThings.ThingsOfDef(def); // 枚举操作异常风险
```

### ✅ 使用SafeAccessService
```csharp
// 正确方式 - 内置重试和异常处理
var colonists = await CoreServices.SafeAccess.GetColonistsSafeAsync(map);
var items = await CoreServices.SafeAccess.GetThingsSafeAsync(map, def);
```

### ❌ 直接单例调用
```csharp
// 不要这样做
var data = Governor.Instance.GetData();
```

### ✅ 使用服务容器
```csharp
// 正确方式
var governor = CoreServices.Governor;
if (governor != null)
{
    var data = await governor.GetDataAsync();
}
```

### ❌ 阻塞UI线程
```csharp
// 不要在UI中这样做
var result = myAsyncMethod().Result; // 会卡死UI
```

### ✅ 正确的异步处理
```csharp
// UI中异步调用
async void OnButtonClick()
{
    var result = await myAsyncMethod();
    UpdateUI(result);
}
```

## 🎯 下一步

- 📖 阅读 [架构设计文档](ARCHITECTURE.md) 了解设计原理
- 👨‍💻 查看 [开发者指南](DEVELOPER_GUIDE.md) 学习深入开发
- 📚 参考 [API手册](API_REFERENCE.md) 查找具体用法
- 🎮 运行示例查看实际效果

## 💡 小贴士

1. **始终使用CoreServices**: 这是企业级架构的正确方式
2. **使用SafeAccess访问RimWorld API**: 避免并发修改异常，内置重试机制
3. **合理使用缓存**: 避免重复的昂贵AI调用
4. **异步为主**: 所有AI调用都应该是异步的
5. **事件驱动**: 使用EventBus实现组件解耦
6. **日志记录**: 便于调试和问题排查

---
*🎯 现在你已经掌握了RimAI开发的基础！开始创建你的第一个AI官员吧！*
