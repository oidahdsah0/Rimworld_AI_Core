# RimAI Core 新 API 使用示例

## 基础使用

### 简单消息发送
```csharp
using RimAI.Framework.API;

// 检查框架状态
if (RimAIAPI.IsInitialized)
{
    string response = await RimAIAPI.SendMessageAsync("分析当前殖民地状态");
    Log.Message($"AI 响应: {response}");
}
```

### 流式消息处理
```csharp
await RimAIAPI.SendStreamingMessageAsync(
    "提供实时建议",
    chunk => {
        // 处理每个数据块
        currentResponse.Append(chunk);
        UpdateUI(currentResponse.ToString());
    }
);
```

## 使用选项预设

### 不同场景的选项
```csharp
// 创意性回答（高温度）
var creativeOptions = RimAIAPI.Options.Creative(temperature: 1.2);
string story = await RimAIAPI.SendMessageAsync("创建一个事件故事", creativeOptions);

// 事实性分析（低温度）
var factualOptions = RimAIAPI.Options.Factual(temperature: 0.2);
string analysis = await RimAIAPI.SendMessageAsync("分析资源数据", factualOptions);

// 强制流式模式
var streamingOptions = RimAIAPI.Options.Streaming(temperature: 0.8);
await RimAIAPI.SendStreamingMessageAsync("实时更新", onUpdate, streamingOptions);

// JSON 格式响应
var jsonOptions = RimAIAPI.Options.Json(temperature: 0.5);
string jsonResponse = await RimAIAPI.SendMessageAsync("返回JSON格式的分析", jsonOptions);
```

## 使用高级服务

### JSON 服务
```csharp
var jsonService = RimAIAPI.GetJsonService();
if (jsonService != null)
{
    var result = await jsonService.SendJsonRequestAsync<MyDataType>(
        "分析并返回结构化数据",
        RimAIAPI.Options.Json()
    );
    
    if (result.Success)
    {
        var data = result.Data;
        // 使用结构化数据
    }
}
```

### 自定义服务
```csharp
var customService = RimAIAPI.GetCustomService();
if (customService != null)
{
    var customOptions = new LLMRequestOptions
    {
        Temperature = 0.9,
        MaxTokens = 2000,
        EnableStreaming = true
    };
    
    string response = await customService.SendCustomRequestAsync(prompt, customOptions);
}
```

## 官员系统使用

### 总督服务
```csharp
using RimAI.Core.Officers;

// 结构化分析
var analysis = await Governor.Instance.AnalyzeColonyAsync();
if (analysis != null)
{
    Log.Message($"殖民者数量: {analysis.TotalColonists}");
    Log.Message($"平均心情: {analysis.AverageMood}");
}

// 高级建议
string advice = await Governor.Instance.GetAdvancedRecommendationAsync();

// 实时建议
await Governor.Instance.GetRealTimeAdviceAsync(partialResponse => {
    UpdateUI(partialResponse);
});
```

### 物流官员
```csharp
using RimAI.Core.Officers;

// 资源分析
string resourceAnalysis = await LogisticsOfficer.Instance.AnalyzeResourcesAsync();

// 生产建议
string productionAdvice = await LogisticsOfficer.Instance.GetProductionAdviceAsync();

// 实时库存监控
await LogisticsOfficer.Instance.MonitorInventoryAsync(update => {
    ShowInventoryAlert(update);
});
```

### 军事官员
```csharp
using RimAI.Core.Officers;

// 威胁评估
string threatAssessment = await MilitaryOfficer.Instance.AssessThreatLevelAsync();

// 实时战术建议
await MilitaryOfficer.Instance.GenerateTacticalAdviceAsync(
    partialAdvice => UpdateTacticalDisplay(partialAdvice),
    cancellationToken
);

// 结构化战斗分析
var combatAnalysis = await MilitaryOfficer.Instance.AnalyzeCombatCapabilityAsync();
if (combatAnalysis != null)
{
    Log.Message($"战斗评级: {combatAnalysis.CombatRating}/100");
    Log.Message($"战备等级: {combatAnalysis.ReadinessLevel}");
}

// 紧急响应
string emergencyResponse = await MilitaryOfficer.Instance.EmergencyCombatResponseAsync("敌军围攻");
```

## 智能总督系统

### SmartGovernor 高级用法
```csharp
using RimAI.Core.AI;

// 快速决策（适合紧急情况）
string quickDecision = await SmartGovernor.Instance.GetQuickDecision("粮食短缺危机");

// 详细策略（适合长期规划）
string detailedStrategy = await SmartGovernor.Instance.GetDetailedStrategy(colonyStatus, cancellationToken);

// 实时事件解说
await SmartGovernor.Instance.GetEventNarration(
    "陨石撞击事件",
    partialNarration => DisplayNarration(partialNarration),
    cancellationToken
);
```

## 错误处理最佳实践

### 统一错误处理
```csharp
public async Task<string> SafeAICall(string prompt)
{
    try
    {
        // 1. 检查框架状态
        if (!RimAIAPI.IsInitialized)
        {
            Log.Warning("RimAI Framework 未初始化");
            return "AI 服务不可用";
        }

        // 2. 执行 API 调用
        string response = await RimAIAPI.SendMessageAsync(prompt);
        
        // 3. 验证响应
        return response ?? "无法获取 AI 响应";
    }
    catch (OperationCanceledException)
    {
        Log.Message("AI 请求被用户取消");
        return "操作已取消";
    }
    catch (Exception ex)
    {
        Log.Error($"AI 请求失败: {ex.Message}");
        return $"请求失败: {ex.Message}";
    }
}
```

### 取消操作支持
```csharp
private CancellationTokenSource _currentOperation;

public async Task<string> LongRunningAITask(string prompt)
{
    // 取消之前的操作
    _currentOperation?.Cancel();
    _currentOperation = new CancellationTokenSource();
    
    try
    {
        return await RimAIAPI.SendMessageAsync(prompt, _currentOperation.Token);
    }
    catch (OperationCanceledException)
    {
        return "操作已取消";
    }
    finally
    {
        _currentOperation?.Dispose();
        _currentOperation = null;
    }
}

public void CancelCurrentOperation()
{
    _currentOperation?.Cancel();
}
```

## 性能优化建议

### 选择合适的 API 模式
```csharp
// UI 场景：优先使用流式 API 提供实时反馈
if (isUIContext && RimAIAPI.IsStreamingEnabled)
{
    await RimAIAPI.SendStreamingMessageAsync(prompt, UpdateUI);
}
else
{
    // 后台处理：使用标准 API
    string response = await RimAIAPI.SendMessageAsync(prompt);
}
```

### 缓存和批处理
```csharp
// 避免频繁的小请求，考虑批处理
var batchPrompt = $@"请分析以下多个问题：
1. {question1}
2. {question2}  
3. {question3}";

string batchResponse = await RimAIAPI.SendMessageAsync(batchPrompt);
```

## 集成到现有 Mod

### 在现有 Mod 中集成 Core 功能
```csharp
// 检查 Core 是否可用
public static bool IsCoreAvailable()
{
    try
    {
        return RimAI.Core.RimAICoreGameComponent.IsFrameworkAvailable();
    }
    catch
    {
        return false;
    }
}

// 条件性使用 Core 功能
if (IsCoreAvailable())
{
    var advice = await Governor.Instance.GetAdvancedRecommendationAsync();
    // 使用 AI 建议
}
else
{
    // 使用传统逻辑
}
```

这些示例展示了如何充分利用升级后的 RimAI Core 系统的各种功能。新的架构提供了更大的灵活性和更好的性能，同时保持了易用性。
