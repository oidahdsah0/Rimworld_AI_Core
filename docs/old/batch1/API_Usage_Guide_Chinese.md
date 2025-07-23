# RimAI Core API 调用指南

**版本**: 1.0  
**作者**: [@oidahdsah0](https://github.com/oidahdsah0)  
**更新时间**: 2025年7月

---

## 📋 概述

RimAI Core 建立在 RimAI Framework 之上，为实现高级游戏内 AI 功能提供了专门的 API。本文档将指导你如何利用 Core 模块与 Framework 进行交互，以实现复杂的殖民地管理逻辑。

## 🛠️ 快速开始

### 1. 确保依赖关系

在你的项目中，首先需要确保 `Rimworld_AI_Core` 已经正确依赖了 `Rimworld_AI_Framework`。

#### 在 `About.xml` 中确认依赖：

```xml
<ModMetaData>
  <!-- ... 其他元数据 ... -->
  <modDependencies>
    <li>
      <packageId>oidahdsah0.RimAI.Framework</packageId>
      <displayName>RimAI Framework</displayName>
      <steamWorkshopUrl>...</steamWorkshopUrl>
    </li>
  </modDependencies>
</ModMetaData>
```

#### 在项目文件中添加 Framework DLL 引用：

```xml
<ItemGroup>
  <!-- 项目引用（当两个项目在同一解决方案中时） -->
  <ProjectReference Include="..\..\Rimworld_AI_Framework\RimAI.Framework\RimAI.Framework.csproj" Condition="Exists('..\..\Rimworld_AI_Framework\RimAI.Framework\RimAI.Framework.csproj')">
    <Private>false</Private>
  </ProjectReference>
  
  <!-- DLL引用（用于独立开发时） -->
  <Reference Include="RimAI.Framework" Condition="!Exists('..\..\Rimworld_AI_Framework\RimAI.Framework\RimAI.Framework.csproj')">
    <HintPath>..\..\Rimworld_AI_Framework\RimAI.Framework\Assemblies\RimAI.Framework.dll</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

这种配置确保了：
- 当两个项目都在同一个解决方案中时，使用项目引用
- 当只开发 Core 项目时，使用预编译的 Framework DLL

### 2. 导入核心命名空间

在你的 C# 文件中，导入 `RimAI.Core` 和 `RimAI.Framework` 的 API 命名空间：

```csharp
using RimAI.Core;
using RimAI.Framework.API;
using System.Threading.Tasks;
using Verse;
```

## 🎯 RimAI Core 如何调用 Framework

`RimAI.Core` 内部封装了对 `RimAI.Framework` 的调用。其主要逻辑位于 `RimAICoreMod.cs` 和 `RimAICoreSettings.cs` 中。

### 核心调用流程

1.  **用户或游戏事件触发**：例如，玩家通过 UI 下达指令，或者游戏内发生特定事件（如袭击）。
2.  **构建提示 (Prompt)**：`RimAI.Core` 中的逻辑模块（如 `Governor`, `MilitaryOfficer`）会根据当前游戏状态和目标构建一个详细的文本提示。
3.  **调用 Framework API**：Core 模块使用 `RimAIApi.GetChatCompletion(prompt)` 方法将构建好的提示发送给 Framework。
4.  **Framework 处理请求**：Framework 将请求加入处理队列，并发送给配置好的 LLM 服务。
5.  **接收和解析响应**：Core 模块接收到来自 Framework 的 LLM 响应字符串。
6.  **执行动作**：Core 模块解析响应，并将其转换为游戏内的具体行动，例如创建指令、分配任务或调整策略。

### 示例：总督（Governor）的决策流程

以下是一个简化的示例，展示了总督如何使用 Framework 来做出决策。

```csharp
// 在 RimAI.Core 的某个类中

public class GovernorLogic
{
    public async Task ManageColony()
    {
        // 1. 收集殖民地状态信息
        string colonyStatus = GetColonyStatus(); // 获取资源、殖民者心情等信息

        // 2. 构建决策提示
        string prompt = $"作为殖民地总督，根据以下状态做出下一个优先事项的决策：\n{colonyStatus}";

        try
        {
            // 3. 调用 Framework API
            string decision = await RimAIApi.GetChatCompletion(prompt);

            if (!string.IsNullOrEmpty(decision))
            {
                // 4. 解析并执行决策
                Log.Message($"总督决策: {decision}");
                ExecuteDecision(decision);
            }
            else
            {
                Log.Warning("总督未能做出决策。");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"总督决策时发生错误: {ex.Message}");
        }
    }

    private string GetColonyStatus()
    {
        // ... 实现获取殖民地状态的逻辑 ...
        return "食物储备低，一个殖民者心情不佳，防御工事薄弱。";
    }

    private void ExecuteDecision(string decision)
    {
        // ... 实现解析和执行决策的逻辑 ...
        // 例如：如果决策是“优先增加食物生产”，则调整工作优先级
    }
}
```

## 💡 在 Core 中扩展功能的建议

如果你想在 `RimAI.Core` 中添加新的功能，可以遵循以下步骤：

1.  **创建新的逻辑类**：例如，创建一个 `DiplomacyOfficer` 类来处理外交事务。
2.  **定义提示构建逻辑**：在新类中，编写方法来根据外交情况构建不同的提示。
3.  **调用 Framework**：使用 `RimAIApi.GetChatCompletion` 来获取 LLM 的建议。
4.  **实现行动执行**：编写代码来解析 LLM 的响应，并执行相应的外交行动（例如，发送礼物、提议联盟等）。

### 示例：添加一个“研究助手”

```csharp
public class ResearchAssistant
{
    public async Task<string> SuggestNextResearchProject()
    {
        // 获取当前已完成和可用的研究项目
        string researchStatus = GetResearchStatus();

        string prompt = $"根据以下研究状态，推荐下一个最有价值的研究项目，并说明原因：\n{researchStatus}";

        string suggestion = await RimAIApi.GetChatCompletion(prompt);

        if (!string.IsNullOrEmpty(suggestion))
        {
            Messages.Message($"研究助手建议: {suggestion}", MessageTypeDefOf.PositiveEvent);
            return suggestion;
        }

        return "无法获取建议。";
    }

    private string GetResearchStatus()
    {
        // ... 获取研究状态的逻辑 ...
        return "已完成'电力'，可用项目包括'微电子基础'和'枪械锻造'。";
    }
}
```

## ⚙️ 调试和日志

-   **Framework 日志**：`RimAI.Framework` 会记录所有与 LLM 服务的交互。检查游戏日志可以帮助你了解 API 请求是否成功。
-   **Core 日志**：在 `RimAI.Core` 的代码中添加 `Log.Message` 来跟踪提示的构建、接收到的响应以及执行的动作，这对于调试非常有帮助。

---

## 📞 技术支持

如果你在 `RimAI.Core` 的开发中遇到问题，请：

1.  **确认 Framework 是否正常工作**：可以单独测试 Framework 的 API 调用。
2.  **检查 Core 的提示构建逻辑**：确保你发送给 Framework 的提示是清晰且有意义的。
3.  **查阅 Framework API 文档**：确保你正确使用了 `RimAIApi`。
4.  在 GitHub 仓库中创建 issue。

**GitHub 仓库**: https://github.com/oidahdsah0/Rimworld_AI_Core

---

*本文档旨在帮助你理解 Core 和 Framework 之间的协作方式。*
