# 🏗️ RimAI Core 组件创建完整指南

## 🧭 选择你的使用方式

**我完全是新手，想要最简单的方法**: 
👉 查看 [SIMPLE_GUIDE.md](./SIMPLE_GUIDE.md) - 30秒创建AI助手！

**我是新手，想要稍微了解一下**: 
👉 直接跳到 [⏱️ 5分钟快速教程](#️-5分钟快速教程) 或 [🚀 快速上手模板](#-快速上手模板)

**我想要了解完整功能**: 
👉 从 [🎯 组件类型说明](#-组件类型说明) 开始阅读

**我遇到了问题**: 
👉 查看 [🆘 常见问题](#-常见问题)

---

## 📋 概述

本指南详细介绍如何在 RimAI Core 项目中创建新的组件，包括 AI 官员、分析器、服务和工作流。

⚠️ **新手友好提醒**: 如果你觉得这个指南太复杂，可以直接跳到 [🚀 快速上手模板](#-快速上手模板) 部分！

---

## 🚀 快速上手模板

### 😊 我只想创建一个简单的AI官员

**最简单的AI官员模板** (复制粘贴即可使用)：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Officers.Base;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 我的第一个AI官员 - 复制这个模板开始你的开发！
    /// </summary>
    public class MyFirstOfficer : OfficerBase
    {
        // 单例模式 - 不用改，直接复制
        private static MyFirstOfficer _instance;
        public static MyFirstOfficer Instance => _instance ??= new MyFirstOfficer();
        
        // 修改这些信息为你的官员信息
        public override string Name => "我的AI助手";
        public override string Description => "一个简单的AI助手";
        public override string IconPath => "UI/Icons/Governor"; // 可以用现有图标
        public override OfficerRole Role => OfficerRole.Governor; // 选择一个角色

        // 私有构造函数 - 不用改
        private MyFirstOfficer() { }

        // 这里是关键：告诉AI你的游戏情况
        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var context = new Dictionary<string, object>();
            
            // 添加你想让AI知道的信息
            var map = Find.CurrentMap;
            if (map != null)
            {
                context["殖民者数量"] = map.mapPawns.FreeColonistsCount;
                context["当前季节"] = GenLocalDate.Season(map).ToString();
                context["天气"] = map.weatherManager.curWeather.label;
                
                // 你可以在这里添加更多信息
                // context["你的数据"] = "你的值";
            }
            
            return context;
        }
    }
}
```

**然后只需两步就能使用**:

1. **注册你的官员** (在ServiceContainer.cs中添加一行):
```csharp
RegisterInstance<MyFirstOfficer>(MyFirstOfficer.Instance);
```

2. **在UI中添加按钮** (在MainTabWindow_RimAI.cs中):
```csharp
if (Widgets.ButtonText(new Rect(x, y, 200, 30), "我的AI助手"))
{
    var advice = await MyFirstOfficer.Instance.GetAdviceAsync();
    // 显示建议
}
```

**就这么简单！** 🎉

---

### 😊 我想要更简单的数据分析器

**超简单分析器模板**:

```csharp
using System;
using Verse;

namespace RimAI.Core.Analysis
{
    /// <summary>
    /// 我的简单分析器 - 分析游戏中的某个方面
    /// </summary>
    public class MySimpleAnalyzer
    {
        // 单例模式
        private static MySimpleAnalyzer _instance;
        public static MySimpleAnalyzer Instance => _instance ??= new MySimpleAnalyzer();
        private MySimpleAnalyzer() { }

        /// <summary>
        /// 分析某个方面并返回简单结果
        /// </summary>
        public string AnalyzeSomething()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return "没有地图";

                // 这里写你的分析逻辑
                var colonistCount = map.mapPawns.FreeColonistsCount;
                
                if (colonistCount < 3)
                    return "殖民者太少了！";
                else if (colonistCount > 10)
                    return "殖民者很多，管理要小心！";
                else
                    return "殖民者数量正常";
            }
            catch (Exception ex)
            {
                Log.Error($"分析失败: {ex.Message}");
                return "分析失败";
            }
        }
    }
}
```

---

## ⏱️ 5分钟快速教程

**目标**: 5分钟内创建一个能工作的AI助手

### 步骤1: 复制代码 (2分钟)
1. 打开 `Source/Officers/` 文件夹
2. 创建文件 `MyHelper.cs`
3. 复制粘贴这段代码：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Officers.Base;

namespace RimAI.Core.Officers
{
    public class MyHelper : OfficerBase
    {
        private static MyHelper _instance;
        public static MyHelper Instance => _instance ??= new MyHelper();
        
        public override string Name => "小助手";
        public override string Description => "我的第一个AI助手";
        public override string IconPath => "UI/Icons/Governor";
        public override OfficerRole Role => OfficerRole.Governor;

        private MyHelper() { }

        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var context = new Dictionary<string, object>();
            var map = Find.CurrentMap;
            if (map != null)
            {
                context["殖民者数量"] = map.mapPawns.FreeColonistsCount;
                context["天气"] = map.weatherManager.curWeather.label;
            }
            return context;
        }
    }
}
```

### 步骤2: 注册服务 (1分钟)
1. 打开 `Source/Architecture/ServiceContainer.cs`
2. 在 `RegisterDefaultServices()` 方法中添加一行：

```csharp
RegisterInstance<MyHelper>(MyHelper.Instance);
```

### 步骤3: 添加UI按钮 (2分钟)
1. 打开 `UI/MainTabWindow_RimAI.cs`
2. 找到 `DoWindowContents` 方法
3. 添加按钮代码：

```csharp
if (Widgets.ButtonText(new Rect(10, 100, 200, 30), "我的AI助手"))
{
    var advice = await MyHelper.Instance.GetAdviceAsync();
    Messages.Message(advice, MessageTypeDefOf.NeutralEvent);
}
```

**完成！** 重新编译，进游戏就能看到你的AI助手了！🎉

---

## 🤝 复杂度层次说明

我们设计了三个复杂度层次，你可以选择适合自己的：

### 🟢 初级 - 直接复制模板 (推荐新手)
- ✅ 使用上面的简单模板
- ✅ 只需要修改几个字符串
- ✅ 不需要理解复杂架构
- ✅ 5分钟就能有结果

### 🟡 中级 - 理解基本概念  
- 学习异步编程基础
- 了解依赖注入的好处
- 参考Governor的实现

### 🔴 高级 - 完全自定义
- 实现复杂的分析逻辑
- 创建自定义服务和工作流
- 深度集成Framework功能

---

## 🎯 组件类型说明

### 1️⃣ AI 官员 (Officers)
- **位置**: `Source/Officers/`
- **继承**: `OfficerBase`
- **用途**: 提供特定领域的 AI 建议和分析
- **示例**: `ResearchOfficer.cs` (科研官员)

### 2️⃣ 分析器 (Analyzers)
- **位置**: `Source/Analysis/`
- **模式**: 单例模式
- **用途**: 分析游戏数据，生成结构化报告
- **示例**: `SecurityAnalyzer.cs` (安全分析器)

### 3️⃣ 服务 (Services)
- **位置**: `Source/Services/`
- **模式**: 单例模式
- **用途**: 提供系统级功能和支持
- **示例**: `AutomationService.cs` (自动化任务服务)

### 4️⃣ 工作流 (Workflows)
- **位置**: `Source/AI/`
- **模式**: 单例模式
- **用途**: 复杂的自动化业务流程
- **示例**: `CrisisManagementWorkflow.cs` (危机管理工作流)

---

## 🔧 创建步骤详解

### 步骤 1: 确定组件类型和设计

首先明确你要创建什么类型的组件：

```csharp
// 选择合适的基类或模式
// 官员 → 继承 OfficerBase
// 分析器 → 单例模式，直接分析数据
// 服务 → 单例模式，提供系统功能
// 工作流 → 单例模式，复杂业务逻辑
```

### 步骤 2: 创建核心类

#### 2.1 AI 官员模板

```csharp
using RimAI.Core.Officers.Base;

namespace RimAI.Core.Officers
{
    public class YourOfficer : OfficerBase
    {
        private static YourOfficer _instance;
        public static YourOfficer Instance => _instance ??= new YourOfficer();

        public override string Name => "你的官员名称";
        public override string Description => "官员描述";
        public override string IconPath => "UI/Icons/YourIcon";
        public override OfficerRole Role => OfficerRole.YourRole;

        private YourOfficer() { }

        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var context = await base.BuildContextAsync(cancellationToken);
            
            // 添加你的特定上下文
            context["YourData"] = GetYourSpecificData();
            
            return context;
        }

        // 你的特定方法
        private object GetYourSpecificData()
        {
            // 实现数据收集逻辑
            return new { };
        }
    }
}
```

#### 2.2 分析器模板 (现代异步版本)

```csharp
namespace RimAI.Core.Analysis
{
    public class YourAnalyzer : IColonyAnalyzer  // 实现现代异步接口
    {
        private static YourAnalyzer _instance;
        public static YourAnalyzer Instance => _instance ??= new YourAnalyzer();

        private YourAnalyzer() { }

        // 主要分析方法 - 异步版本
        public async Task<YourAnalysisResult> AnalyzeYourDomainAsync(CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var result = new YourAnalysisResult();
                
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return CreateEmptyResult();

                    // 执行你的分析逻辑
                    result.YourMetric = CalculateYourMetric(map);
                    result.YourStatus = EvaluateYourStatus(map);
                    
                    Log.Message($"[YourAnalyzer] 分析完成: {result.YourMetric}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[YourAnalyzer] Analysis failed: {ex.Message}");
                    result.ErrorMessage = ex.Message;
                }

                return result;
            }, cancellationToken);
        }

        // 实现 IColonyAnalyzer 接口方法
        public async Task<ColonyAnalysisResult> AnalyzeColonyAsync(CancellationToken cancellationToken = default)
        {
            // 如果你的分析器专门分析特定领域，可以调用你的专业方法
            var yourResult = await AnalyzeYourDomainAsync(cancellationToken);
            
            // 转换为通用格式或返回空结果
            return CreateEmptyAnalysisResult("专业分析器 - 请使用专门方法");
        }

        public async Task<string> GetQuickStatusSummaryAsync(CancellationToken cancellationToken = default)
        {
            var analysis = await AnalyzeYourDomainAsync(cancellationToken);
            return $"你的领域状态: {analysis.YourStatus} (指标: {analysis.YourMetric})";
        }

        public async Task<T> GetSpecializedAnalysisAsync<T>(CancellationToken cancellationToken = default) where T : class
        {
            if (typeof(T) == typeof(YourAnalysisResult))
                return await AnalyzeYourDomainAsync(cancellationToken) as T;
            
            return null;
        }

        // 你的分析方法
        private int CalculateYourMetric(Map map) => 0;
        private string EvaluateYourStatus(Map map) => "Unknown";
        private YourAnalysisResult CreateEmptyResult() => new YourAnalysisResult();
        private ColonyAnalysisResult CreateEmptyAnalysisResult(string message) => new ColonyAnalysisResult { ErrorMessage = message };
    }

    // 现代化数据模型
    public class YourAnalysisResult
    {
        public int YourMetric { get; set; }
        public string YourStatus { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public string ErrorMessage { get; set; }
    }
}
```
```

#### 2.3 服务模板

```csharp
namespace RimAI.Core.Services
{
    public class YourService
    {
        private static YourService _instance;
        public static YourService Instance => _instance ??= new YourService();

        public event Action<string> YourEvent;

        private YourService() { }

        public async Task<bool> DoYourOperationAsync(string parameter)
        {
            try
            {
                // 执行你的操作
                await YourAsyncLogic(parameter);
                
                YourEvent?.Invoke($"Operation completed: {parameter}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[YourService] Operation failed: {ex.Message}");
                return false;
            }
        }

        private async Task YourAsyncLogic(string parameter)
        {
            // 你的异步逻辑
            await Task.Delay(100);
        }
    }
}
```

#### 2.4 工作流模板

```csharp
namespace RimAI.Core.AI
{
    public class YourWorkflow
    {
        private static YourWorkflow _instance;
        public static YourWorkflow Instance => _instance ??= new YourWorkflow();

        private bool _isActive = false;
        private CancellationTokenSource _workflowCts;

        public bool IsActive => _isActive;
        public event Action<YourWorkflowEvent> WorkflowEvent;

        private YourWorkflow() { }

        public async Task<bool> StartWorkflowAsync()
        {
            try
            {
                if (_isActive) return false;

                _workflowCts = new CancellationTokenSource();
                _isActive = true;

                _ = Task.Run(() => WorkflowLoop(_workflowCts.Token));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[YourWorkflow] Failed to start: {ex.Message}");
                return false;
            }
        }

        public void StopWorkflow()
        {
            _workflowCts?.Cancel();
            _isActive = false;
        }

        private async Task WorkflowLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteWorkflowStep();
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"[YourWorkflow] Loop error: {ex.Message}");
                }
            }
        }

        private async Task ExecuteWorkflowStep()
        {
            // 你的工作流逻辑
        }
    }

    public class YourWorkflowEvent
    {
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
```

### 步骤 3: 注册到服务容器

⚠️ **重要提醒**: 分析器服务已重新启用！ColonyAnalyzer 现在完全可用。

在 `ServiceContainer.cs` 的 `RegisterDefaultServices` 方法中添加：

```csharp
private void RegisterDefaultServices()
{
    // 核心服务 (✅ 已恢复)
    RegisterInstance<IColonyAnalyzer>(ColonyAnalyzer.Instance); // 重新启用分析器
    RegisterInstance<ILLMService>(LLMService.Instance);
    RegisterInstance<IPromptBuilder>(PromptBuilder.Instance);
    RegisterInstance<ICacheService>(CacheService.Instance);
    
    // 注册你的新组件
    RegisterInstance<YourAnalyzer>(YourAnalyzer.Instance);
    RegisterInstance<YourService>(YourService.Instance);
    RegisterInstance<YourWorkflow>(YourWorkflow.Instance);
    
    Log.Message("[ServiceContainer] Default services registered with ColonyAnalyzer enabled");
}
```

### 步骤 4: 添加到 CoreServices 访问器

在 `ServiceContainer.cs` 的 `CoreServices` 类中添加：

```csharp
public static class CoreServices
{
    // 核心服务 (✅ 已恢复)
    public static IColonyAnalyzer Analyzer => ServiceContainer.Instance.GetService<IColonyAnalyzer>(); // 重新启用
    public static ILLMService LLMService => ServiceContainer.Instance.GetService<ILLMService>();
    public static IPromptBuilder PromptBuilder => ServiceContainer.Instance.GetService<IPromptBuilder>();
    
    // 你的新服务
    public static YourAnalyzer YourAnalyzer => ServiceContainer.Instance.GetService<YourAnalyzer>();
    public static YourService YourService => ServiceContainer.Instance.GetService<YourService>();
    public static YourWorkflow YourWorkflow => ServiceContainer.Instance.GetService<YourWorkflow>();
}
```

### 步骤 5: 集成到 UI

在 `MainTabWindow_RimAI.cs` 或创建新的 UI 窗口中集成你的组件：

```csharp
// 在 DoWindowContents 方法中添加按钮
if (Widgets.ButtonText(new Rect(x, y, 200, 30), "你的功能"))
{
    var result = await YourOfficer.Instance.GetAdviceAsync();
    // 显示结果
}
```

### 步骤 6: 添加配置支持

在 `CoreSettings.cs` 中添加相关设置：

```csharp
public class CoreSettings : ModSettings
{
    // 现有设置...
    
    // 你的设置
    public bool enableYourFeature = true;
    public float yourParameter = 1.0f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref enableYourFeature, "enableYourFeature", true);
        Scribe_Values.Look(ref yourParameter, "yourParameter", 1.0f);
    }
}
```

---

## ⚙️ 最佳实践

### 🔒 单例模式规范

```csharp
private static YourClass _instance;
public static YourClass Instance => _instance ??= new YourClass();

private YourClass() { } // 私有构造函数
```

### 📊 数据模型设计

```csharp
public class YourDataModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    
    // 你的属性
}
```

### 🛡️ 异常处理模式

```csharp
try
{
    // 你的逻辑
}
catch (Exception ex)
{
    Log.Error($"[YourClass] Operation failed: {ex.Message}");
    return GetDefaultValue();
}
```

### ⚡ 性能优化

```csharp
// 使用缓存
private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

// 异步操作
public async Task<T> DoOperationAsync<T>(CancellationToken cancellationToken = default)
{
    // 实现
}

// 延迟加载
private Lazy<ExpensiveObject> _expensiveObject = new Lazy<ExpensiveObject>(() => new ExpensiveObject());
```

---

## 🧪 测试你的组件

### 基本测试

```csharp
// 测试实例创建
var instance = YourClass.Instance;
Assert.IsNotNull(instance);

// 测试主要功能
var result = await instance.YourMainMethod();
Assert.IsTrue(result);
```

### 集成测试

1. 在游戏中测试基本功能
2. 测试异常情况处理
3. 测试性能和内存使用
4. 测试与其他组件的集成

---

## 📝 文档规范

### 类注释

```csharp
/// <summary>
/// 你的类的简要描述 - 主要功能和用途
/// </summary>
public class YourClass
```

### 方法注释

```csharp
/// <summary>
/// 方法功能描述
/// </summary>
/// <param name="parameter">参数描述</param>
/// <param name="cancellationToken">取消令牌</param>
/// <returns>返回值描述</returns>
public async Task<ReturnType> YourMethod(string parameter, CancellationToken cancellationToken = default)
```

---

## 🚀 部署检查清单

- [ ] 组件正确继承基类或实现接口
- [ ] 单例模式正确实现
- [ ] 异常处理完整
- [ ] 日志记录充分
- [ ] 已注册到服务容器
- [ ] UI 集成完成
- [ ] 设置项添加
- [ ] 基本测试通过
- [ ] 文档注释完整
- [ ] 代码风格一致

---

## 🆘 常见问题

**Q: 这个框架看起来太复杂了，我能简化使用吗？**
A: **绝对可以！** 请查看文档开头的 [🚀 快速上手模板](#-快速上手模板) 部分。你可以：
- 直接复制粘贴简单模板
- 不需要理解复杂的异步编程
- 只修改几个字符串就能工作
- 忽略所有高级功能，专注于你的逻辑

**Q: 我不懂async/await，能用同步的方式吗？**
A: 可以！虽然我们推荐异步，但你可以这样简化：
```csharp
protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
{
    var context = new Dictionary<string, object>();
    
    // 你的简单逻辑（同步的）
    context["我的数据"] = GetMySimpleData(); // 普通方法，不用async
    
    return context;
}

// 普通的同步方法
private string GetMySimpleData()
{
    return "我的简单数据";
}
```

**Q: ServiceContainer是什么？我必须学会吗？**
A: 不用完全理解！只需要记住两个步骤：
1. 添加这行代码注册：`RegisterInstance<你的类>(你的类.Instance);`
2. 然后就能在任何地方使用：`你的类.Instance.你的方法()`

**Q: 我只想要一个简单的AI对话功能，需要这么复杂吗？**
A: 不需要！最简单的AI调用：
```csharp
// 直接调用AI（绕过所有复杂架构）
var response = await RimAIAPI.SendMessageAsync("你的问题");
```

**Q: 如何调试我的组件？**
A: 使用 `Log.Message()` 和 `Log.Error()` 记录调试信息，在游戏的开发者控制台中查看。

**Q: 组件之间如何通信？**
A: 最简单的方式是直接调用：`其他组件.Instance.方法()`。不需要复杂的事件系统。

**Q: 如何处理游戏保存/加载？**
A: 简单数据用静态变量就行，复杂数据才需要实现 `IExposable` 接口。

**Q: 性能优化有什么建议？**
A: 使用缓存、避免频繁的游戏数据查询、合理使用异步操作。

---

## 📚 参考示例

本指南中创建的示例组件：
- ✅ `ResearchOfficer.cs` - AI 科研官员
- ✅ `SecurityAnalyzer.cs` - 安全状况分析器  
- ✅ `AutomationService.cs` - 自动化任务服务
- ✅ `CrisisManagementWorkflow.cs` - 危机管理工作流

你可以参考这些组件的实现，作为创建新组件的模板。

---

## 🎯 成功案例：Governor + ColonyAnalyzer 集成

### 完整的分析器-官员集成示例

**ColonyAnalyzer** (分析器层)：
- ✅ 实现现代异步 `IColonyAnalyzer` 接口
- ✅ 提供人口、资源、威胁、基础设施分析
- ✅ 支持 `async/await` 和 `CancellationToken`
- ✅ 结构化数据输出 (`ColonyAnalysisResult`)

**Governor** (官员层)：
- ✅ 继承 `OfficerBase` 获得完整的AI能力
- ✅ 集成 `ColonyAnalyzer` 获得数据分析支持
- ✅ 实现增强的上下文构建 (`BuildContextAsync`)
- ✅ 提供智能决策和建议生成

**ServiceContainer** (服务层)：
- ✅ 完整的服务注册和依赖注入
- ✅ 统一的服务访问器 (`CoreServices.Analyzer`)
- ✅ 服务健康检查和状态报告

### 关键集成代码片段

```csharp
// Governor 使用分析器数据增强决策
public async Task<Dictionary<string, object>> GetContextDataAsync(CancellationToken cancellationToken = default)
{
    var context = new Dictionary<string, object>();
    var analyzer = ColonyAnalyzer.Instance;
    
    // 获取完整分析数据
    var fullAnalysis = await analyzer.AnalyzeColonyAsync(cancellationToken);
    
    // 集成到决策上下文
    context["healthyColonists"] = fullAnalysis.PopulationData.HealthyColonists;
    context["threatLevel"] = fullAnalysis.ThreatData.OverallThreatLevel;
    context["resourceStatus"] = fullAnalysis.ResourceData.FoodDaysRemaining;
    
    return context;
}
```

这个成功案例展示了如何创建一个完整的、现代化的AI官员系统，为其他开发者提供了可参考的实现模板。
