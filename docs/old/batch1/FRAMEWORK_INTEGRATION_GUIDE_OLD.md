# 🔗 RimAI Framework 集成指南

## 📋 概述

本指南详细说明 RimAI Core 如何与 RimAI Framework 集成，包括架构设计、调用链路和最佳实践。

## 🏗️ 架构设计图

### 整体架构
```
┌─────────────────────────────────────────────────────────────┐
│                    RimAI Core Layer                         │
├─────────────────────────────────────────────────────────────┤
│  AI官员 (Officers)  │  分析器 (Analyzers)  │  工作流 (Workflows)  │
│  ├─ Governor ✅     │  ├─ ColonyAnalyzer✅ │  ├─ CrisisManagement │
│  ├─ LogisticsOfficer│  ├─ SecurityAnalyzer │  └─ AutomationFlow   │
│  └─ MilitaryOfficer │  └─ ThreatAnalyzer   │                     │
├─────────────────────────────────────────────────────────────┤
│                    核心服务层 ✅ 完全恢复                      │
│        ┌─────────────────┐  ┌─────────────────┐              │
│        │   LLMService    │  │ ServiceContainer │              │
│        │(ILLMService实现)│  │  + 分析器服务   │ ← 🎯 已恢复    │
│        └─────────────────┘  └─────────────────┘              │
├─────────────────────────────────────────────────────────────┤
│                Framework Wrapper                           │
│              ┌─────────────────────────┐                    │
│              │      RimAI.API          │                    │
│              │   (Framework 入口)       │                    │
│              └─────────────────────────┘                    │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                RimAI Framework Layer                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │   LLM Services  │  │  Configuration  │  │   HTTP Client   │ │
│  │  ├─ OpenAI      │  │  ├─ Settings    │  │  ├─ Request     │ │
│  │  ├─ Claude      │  │  ├─ Providers   │  │  ├─ Response    │ │
│  │  └─ Local       │  │  └─ Security    │  │  └─ Streaming   │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### 调用流程图
```
用户交互 → UI组件 → AI官员 → OfficerBase → LLMService → RimAIAPI → Framework
   ↓         ↓        ↓         ↓          ↓           ↓          ↓
 按钮点击   请求建议   业务逻辑   统一处理   服务封装    API调用   实际执行
```

## 🎯 核心组件说明

### LLMService - Framework 调用的唯一入口

**位置**: `RimAI.Core/Source/Services/LLMService.cs`

**职责**:
- 封装所有对 RimAI Framework 的调用
- 提供统一的错误处理和状态管理
- 支持标准、JSON 和流式请求
- 管理 Framework 的初始化状态

**关键特性**:
```csharp
public class LLMService : ILLMService
{
    // Framework 状态检查
    public bool IsInitialized => RimAIAPI.IsInitialized;
    public bool IsStreamingAvailable => RimAIAPI.IsStreamingEnabled;
    
    // 核心调用方法
    public async Task<string> SendMessageAsync(string prompt, LLMOptions options, CancellationToken cancellationToken)
    public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMOptions options, CancellationToken cancellationToken) 
    public async Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMOptions options, CancellationToken cancellationToken)
}
```

## 📝 实际调用代码追踪

### 步骤1: 用户请求 AI 建议
```csharp
// UI 层调用
var advice = await ResearchOfficer.Instance.GetAdviceAsync();
```

### 步骤2: AI 官员处理 (ResearchOfficer)
```csharp
// ResearchOfficer 继承自 OfficerBase
// 实际调用父类方法
public override async Task<string> GetAdviceAsync(CancellationToken cancellationToken = default)
{
    return await base.GetAdviceAsync(cancellationToken); // 调用父类
}
```

### 步骤3: 基类统一处理 (OfficerBase)
```csharp
public virtual async Task<string> GetAdviceAsync(CancellationToken cancellationToken = default)
{
    // 构建上下文
    var context = await BuildContextAsync(cancellationToken);
    
    // 构建提示词
    var prompt = _promptBuilder.BuildPrompt(templateId, context);
    
    // 📍 关键调用点：通过 LLMService 调用 Framework
    var response = await _llmService.SendMessageAsync(prompt, options, cancellationToken);
    
    return response;
}
```

### 步骤4: LLMService 包装 Framework (关键组件)
```csharp
public async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
{
    if (!IsInitialized)
    {
        throw new InvalidOperationException("RimAI Framework is not initialized");
    }

    // 📍 实际调用 Framework API
    var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
    return response ?? string.Empty;
}
```

### 步骤5: Framework API 执行
```csharp
// RimAI.Framework.API.RimAIAPI
public static async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
{
    // 这里是 Framework 的具体实现
    // 包括：
    // - HTTP 请求构建
    // - 提供商选择 (OpenAI/Claude/Local)
    // - 请求发送
    // - 响应处理
    // - 错误处理
    
    return await executor.ExecuteAsync(request);
}
```

## 🔍 为什么是这种设计？

### 1. **单一责任原则**
- LLMService 专门负责 Framework 调用
- 其他组件专注于业务逻辑

### 2. **统一的错误处理**
```csharp
// 所有 Framework 调用都经过统一的异常处理
try
{
    var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
}
catch (Exception ex)
{
    Log.Error($"[LLMService] Request failed: {ex.Message}");
    throw;
}
```

### 3. **配置和状态管理**
```csharp
public bool IsInitialized => RimAIAPI.IsInitialized;
public bool IsStreamingAvailable => RimAIAPI.IsStreamingEnabled;
```

### 4. **便于测试和模拟**
```csharp
// 可以轻松替换 LLMService 进行测试
RegisterInstance<ILLMService>(MockLLMService.Instance);
```

## 🎯 关键要点

1. **LLMService 是唯一的 Framework 调用者**
2. **所有 AI 功能都通过 LLMService 间接使用 Framework**
3. **这种设计提供了解耦、统一管理和错误处理**
4. **Framework 的复杂性被完全封装在 LLMService 中**

## 📋 调用统计

在当前架构中，以下组件会间接使用 Framework：

- ✅ **AI 官员** (通过 OfficerBase → LLMService)
- ✅ **工作流** (直接调用 LLMService)  
- ✅ **分析器** (如果需要 AI 分析，通过 LLMService)
- ✅ **UI 组件** (通过官员或直接调用 LLMService)

但只有 **LLMService** 直接与 Framework 交互！

## 🔧 集成实现细节

### 1. 服务注册与依赖注入

**ServiceContainer 注册**:
```csharp
// RimAI.Core/Source/Architecture/ServiceContainer.cs
private void RegisterDefaultServices()
{
    // 核心服务
    RegisterInstance<ILLMService>(LLMService.Instance);
    RegisterInstance<IColonyAnalyzer>(ColonyAnalyzer.Instance);
    RegisterInstance<IPromptBuilder>(PromptBuilder.Instance);
    // ... 其他服务
}
```

**依赖注入使用**:
```csharp
// 在 OfficerBase 中
protected OfficerBase()
{
    _llmService = LLMService.Instance;  // 获取 Framework 调用者
    _promptBuilder = PromptBuilder.Instance;
    _analyzer = ColonyAnalyzer.Instance;
    _cacheService = CacheService.Instance;
}
```

### 2. 接口设计模式

**ILLMService 接口**:
```csharp
public interface ILLMService
{
    Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default);
    Task<T> SendJsonRequestAsync<T>(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default) where T : class;
    Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMOptions options = null, CancellationToken cancellationToken = default);
    bool IsStreamingAvailable { get; }
    bool IsInitialized { get; }
}
```

### 3. 错误处理策略

**统一异常处理**:
```csharp
public async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
{
    if (!IsInitialized)
    {
        throw new InvalidOperationException("RimAI Framework is not initialized");
    }

    try
    {
        var response = await RimAIAPI.SendMessageAsync(prompt, options, cancellationToken);
        return response ?? string.Empty;
    }
    catch (OperationCanceledException)
    {
        Log.Message("[LLMService] Request was cancelled");
        throw;
    }
    catch (Exception ex)
    {
        Log.Error($"[LLMService] Request failed: {ex.Message}");
        throw;
    }
}
```

### 4. 缓存策略集成

**官员级别缓存**:
```csharp
public virtual async Task<string> GetAdviceAsync(CancellationToken cancellationToken = default)
{
    var cacheKey = GenerateCacheKey("advice");
    
    return await _cacheService.GetOrCreateAsync(
        cacheKey,
        () => ExecuteAdviceRequest(cancellationToken),
        TimeSpan.FromMinutes(2) // 建议缓存2分钟
    );
}
```

## 🚀 使用示例

### 基础调用示例
```csharp
// 在 UI 或其他组件中
public async void OnGetAdviceButtonClick()
{
    try
    {
        // 通过官员获取建议（推荐方式）
        var advice = await ResearchOfficer.Instance.GetAdviceAsync();
        DisplayAdvice(advice);
    }
    catch (InvalidOperationException ex)
    {
        ShowError($"服务未就绪: {ex.Message}");
    }
    catch (Exception ex)
    {
        ShowError($"获取建议失败: {ex.Message}");
    }
}
```

### 直接服务调用示例
```csharp
// 直接使用 LLMService（高级用法）
public async Task<string> GetCustomAnalysis(string contextData)
{
    var llmService = ServiceContainer.Instance.GetService<ILLMService>();
    
    if (!llmService.IsInitialized)
    {
        return "AI服务未初始化";
    }

    var prompt = $@"请分析以下殖民地数据：
{contextData}

提供具体的改进建议。";

    try
    {
        var options = new LLMOptions { Temperature = 0.7f };
        return await llmService.SendMessageAsync(prompt, options);
    }
    catch (Exception ex)
    {
        Log.Error($"Analysis failed: {ex.Message}");
        return $"分析失败: {ex.Message}";
    }
}
```

### 流式响应示例
```csharp
public async Task GetStreamingAdvice(Action<string> onPartialResponse)
{
    var llmService = LLMService.Instance;
    
    if (!llmService.IsStreamingAvailable)
    {
        onPartialResponse("流式响应不可用，使用标准响应...");
        var result = await llmService.SendMessageAsync(prompt);
        onPartialResponse(result);
        return;
    }

    await llmService.SendStreamingMessageAsync(
        prompt, 
        chunk => onPartialResponse(chunk),  // 实时显示部分响应
        options
    );
}
```

## ⚙️ 配置管理

### Framework 初始化检查
```csharp
public static class FrameworkStatus
{
    public static bool IsReady => LLMService.Instance.IsInitialized;
    
    public static string GetStatusInfo()
    {
        var service = LLMService.Instance;
        return $@"Framework状态:
- 已初始化: {service.IsInitialized}
- 流式支持: {service.IsStreamingAvailable}
- 当前设置: {service.GetCurrentSettings()}";
    }
}
```

### 连接测试
```csharp
public async Task<bool> TestFrameworkConnection()
{
    try
    {
        var (success, message) = await LLMService.Instance.TestConnectionAsync();
        
        if (success)
        {
            Messages.Message("Framework 连接正常", MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            Messages.Message($"Framework 连接失败: {message}", MessageTypeDefOf.RejectInput);
        }
        
        return success;
    }
    catch (Exception ex)
    {
        Log.Error($"Connection test failed: {ex.Message}");
        return false;
    }
}
```

## 🧪 测试与调试

### Mock LLMService 用于测试
```csharp
public class MockLLMService : ILLMService
{
    public bool IsInitialized => true;
    public bool IsStreamingAvailable => false;

    public async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
    {
        // 模拟延迟
        await Task.Delay(100, cancellationToken);
        
        // 返回测试响应
        return $"Mock response for: {prompt.Substring(0, Math.Min(50, prompt.Length))}...";
    }

    public async Task<T> SendJsonRequestAsync<T>(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default) where T : class
    {
        // 返回默认实例或测试数据
        return Activator.CreateInstance<T>();
    }

    public async Task SendStreamingMessageAsync(string prompt, Action<string> onChunk, LLMOptions options = null, CancellationToken cancellationToken = default)
    {
        var mockResponse = "This is a mock streaming response.";
        var words = mockResponse.Split(' ');
        
        foreach (var word in words)
        {
            onChunk?.Invoke(word + " ");
            await Task.Delay(50, cancellationToken);
        }
    }
}

// 在测试中使用
ServiceContainer.Instance.RegisterInstance<ILLMService>(new MockLLMService());
```

### 调试日志配置
```csharp
// 启用详细日志记录
public class VerboseLLMService : LLMService
{
    public override async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
    {
        Log.Message($"[Debug] Sending prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");
        Log.Message($"[Debug] Options: {options?.ToString() ?? "null"}");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await base.SendMessageAsync(prompt, options, cancellationToken);
        stopwatch.Stop();
        
        Log.Message($"[Debug] Response received in {stopwatch.ElapsedMilliseconds}ms, length: {result?.Length ?? 0}");
        
        return result;
    }
}
```

## 🔧 高级集成模式

### 异步队列处理
```csharp
public class QueuedLLMService
{
    private readonly Queue<LLMRequest> _requestQueue = new Queue<LLMRequest>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    
    public async Task<string> QueuedSendMessageAsync(string prompt, LLMOptions options = null)
    {
        var request = new LLMRequest { Prompt = prompt, Options = options };
        var tcs = new TaskCompletionSource<string>();
        request.CompletionSource = tcs;
        
        await _semaphore.WaitAsync();
        try
        {
            _requestQueue.Enqueue(request);
            ProcessQueueAsync(); // 不等待，异步处理
        }
        finally
        {
            _semaphore.Release();
        }
        
        return await tcs.Task;
    }
}
```

### 批量请求处理
```csharp
public async Task<List<string>> SendBatchMessagesAsync(List<string> prompts, LLMOptions options = null)
{
    var tasks = prompts.Select(prompt => 
        LLMService.Instance.SendMessageAsync(prompt, options)
    ).ToArray();
    
    var results = await Task.WhenAll(tasks);
    return results.ToList();
}
```

## 📚 最佳实践

### 1. 错误恢复策略
```csharp
public async Task<string> GetAdviceWithRetry(string prompt, int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await LLMService.Instance.SendMessageAsync(prompt);
        }
        catch (Exception ex) when (attempt < maxRetries)
        {
            Log.Warning($"Attempt {attempt} failed: {ex.Message}, retrying...");
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // 指数退避
        }
    }
    
    throw new Exception($"Failed after {maxRetries} attempts");
}
```

### 2. 资源管理
```csharp
public async Task<string> GetAdviceWithTimeout(string prompt, TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    
    try
    {
        return await LLMService.Instance.SendMessageAsync(prompt, null, cts.Token);
    }
    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
    {
        throw new TimeoutException($"Request timed out after {timeout.TotalSeconds} seconds");
    }
}
```

### 3. 性能监控
```csharp
public class PerformanceMonitoredLLMService : ILLMService
{
    private readonly ILLMService _inner = LLMService.Instance;
    private readonly Dictionary<string, long> _performanceMetrics = new Dictionary<string, long>();
    
    public async Task<string> SendMessageAsync(string prompt, LLMOptions options = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await _inner.SendMessageAsync(prompt, options, cancellationToken);
            return result;
        }
        finally
        {
            stopwatch.Stop();
            RecordMetric("SendMessage", stopwatch.ElapsedMilliseconds);
        }
    }
    
    private void RecordMetric(string operation, long milliseconds)
    {
        var key = $"{operation}_avg_ms";
        _performanceMetrics[key] = (_performanceMetrics.GetValueOrDefault(key) + milliseconds) / 2;
    }
}
```

## 🎯 集成检查清单

- [ ] **LLMService 正确实现 ILLMService 接口**
- [ ] **所有 Framework 调用都经过 LLMService**
- [ ] **错误处理和日志记录完整**
- [ ] **支持取消令牌和超时处理**
- [ ] **Framework 初始化状态检查**
- [ ] **缓存策略正确配置**
- [ ] **单元测试覆盖 Mock 服务**
- [ ] **性能监控和调试支持**
- [ ] **异常情况下的优雅降级**
- [ ] **资源清理和内存管理**

通过这种架构设计，RimAI Core 实现了与 Framework 的松耦合集成，提供了强大而灵活的 AI 功能！
