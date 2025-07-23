# 🚀 RimAI 快速入门

*让您在 5 分钟内完成第一次 AI 调用*

## 前提条件

⚠️ **必须先安装和配置 RimAI Framework**
- 从 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3407451616) 安装 RimAI Framework
- 在 Framework 设置中配置您的 LLM API 密钥
- 确保 RimAI Core 在模组列表中位于 Framework **之后**

## Hello, World! 示例

以下代码演示如何通过 `CoreServices.Governor` 发起最简单的 AI 调用：

```csharp
using Verse;
using RimAI.Core.Architecture;
using System.Threading.Tasks;

public static class MyFirstAICall
{
    public static async void TestGovernor()
    {
        // 1. 检查服务是否就绪
        if (!CoreServices.AreServicesReady())
        {
            Log.Warning("RimAI services not ready. Please check your setup.");
            return;
        }

        // 2. 获取总督并发起 AI 调用
        var governor = CoreServices.Governor;
        
        try
        {
            string response = await governor.ProvideAdviceAsync();
            Log.Message($"AI Governor says: {response}");
        }
        catch (System.Exception ex)
        {
            Log.Error($"AI call failed: {ex.Message}");
        }
    }
}
```

## 如何测试

### 方式1: 开发者模式测试
1. 启用开发者模式 (`Ctrl+F12`)
2. 打开调试日志窗口 (`Ctrl+F12` → Debug log)
3. 在控制台执行: `MyFirstAICall.TestGovernor()`

### 方式2: 添加到您的模组
1. 将代码复制到您的模组项目
2. 在任何地方调用 `MyFirstAICall.TestGovernor()`
3. 查看游戏日志中的 AI 响应

## 核心概念

### 依赖注入门面
```csharp
// ✅ 推荐：通过 CoreServices 门面访问
var governor = CoreServices.Governor;
var llmService = CoreServices.LLMService;
var history = CoreServices.History;

// ❌ 避免：直接使用单例（已废弃）
// var governor = Governor.Instance; // 不再推荐
```

### 服务就绪检查
```csharp
// 始终在使用 AI 功能前检查服务状态
if (!CoreServices.AreServicesReady())
{
    // 处理服务未就绪的情况
    return;
}
```

## 故障排除

**AI 调用失败？**
1. 确认 RimAI Framework 已正确安装并配置 API 密钥
2. 检查 `CoreServices.AreServicesReady()` 返回 `true`
3. 查看游戏日志中的错误信息

**服务未就绪？**
1. 确保模组加载顺序正确（Core 在 Framework 之后）
2. 重启游戏重新初始化服务
3. 检查 Framework 的 API 配置

## 下一步

恭喜！您已经成功完成了第一次 AI 调用。

要深入了解 RimAI 的完整功能，包括：
- 创建自定义 AI 官员
- 构建复杂的对话系统  
- 利用对话历史服务
- 使用提示词工厂服务

请继续阅读：

**📖 [开发者指南](DEVELOPER_GUIDE.md)** - 完整的架构文档和高级开发教程
