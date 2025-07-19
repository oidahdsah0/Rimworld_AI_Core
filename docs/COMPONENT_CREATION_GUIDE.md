# 🏗️ RimAI Core 组件创建完整指南

## 📋 概述

本指南详细介绍如何在 RimAI Core 项目中创建新的组件，包括 AI 官员、分析器、服务和工作流。

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

#### 2.2 分析器模板

```csharp
namespace RimAI.Core.Analysis
{
    public class YourAnalyzer
    {
        private static YourAnalyzer _instance;
        public static YourAnalyzer Instance => _instance ??= new YourAnalyzer();

        private YourAnalyzer() { }

        public YourAnalysisReport AnalyzeYourDomain()
        {
            var report = new YourAnalysisReport();
            
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return CreateEmptyReport();

                // 执行你的分析逻辑
                report.YourMetric = CalculateYourMetric(map);
                report.YourStatus = EvaluateYourStatus(map);
                
            }
            catch (Exception ex)
            {
                Log.Error($"[YourAnalyzer] Analysis failed: {ex.Message}");
            }

            return report;
        }

        // 你的分析方法
        private int CalculateYourMetric(Map map) => 0;
        private string EvaluateYourStatus(Map map) => "Unknown";
        private YourAnalysisReport CreateEmptyReport() => new YourAnalysisReport();
    }

    // 你的数据模型
    public class YourAnalysisReport
    {
        public int YourMetric { get; set; }
        public string YourStatus { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
    }
}
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

在 `ServiceContainer.cs` 的 `RegisterDefaultServices` 方法中添加：

```csharp
private void RegisterDefaultServices()
{
    // 现有服务...
    
    // 注册你的新组件
    RegisterInstance<YourAnalyzer>(YourAnalyzer.Instance);
    RegisterInstance<YourService>(YourService.Instance);
    RegisterInstance<YourWorkflow>(YourWorkflow.Instance);
}
```

### 步骤 4: 添加到 CoreServices 访问器

在 `ServiceContainer.cs` 的 `CoreServices` 类中添加：

```csharp
public static class CoreServices
{
    // 现有服务...
    
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

**Q: 如何调试我的组件？**
A: 使用 `Log.Message()` 和 `Log.Error()` 记录调试信息，在游戏的开发者控制台中查看。

**Q: 组件之间如何通信？**
A: 使用事件系统 (`IEventBus`) 或直接通过服务容器获取其他组件实例。

**Q: 如何处理游戏保存/加载？**
A: 实现 `IExposable` 接口或使用 `GameComponent` 处理数据持久化。

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
