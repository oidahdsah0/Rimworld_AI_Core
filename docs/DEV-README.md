# 🛠️ RimAI Core 开发者README

*面向框架贡献者的完整开发指南*

## 📋 项目概览

RimAI Core 是一个基于依赖注入架构的企业级 RimWorld AI 框架，提供智能殖民地管理和AI官员系统。

**技术栈：**
- .NET Framework 4.8
- C# 9.0+
- RimWorld 1.6+ API
- 依赖注入容器
- 异步编程模型

## 🏗️ 项目文件结构

```
RimAI.Core/
├── About/                          # 模组元数据
│   ├── About.xml                   # 模组信息定义
│   ├── Preview.png                 # 预览图片
│   └── PublishedFileId.txt         # Steam Workshop ID
├── Assemblies/                     # 编译输出目录
├── Defs/                          # 游戏定义文件
├── Languages/                      # 本地化文件
├── loadFolders.xml                # 加载文件夹配置
├── Prompts/                       # 提示词资源
├── Services/                      # 外部服务定义
├── Textures/                      # 贴图资源
│   └── UI/                        # UI相关贴图
├── UI/                            # UI组件
│   └── MainTabWindow_RimAI.cs     # 主UI窗口
├── obj/                           # 构建中间文件
├── RimAI.Core.csproj              # 项目文件
└── Source/                        # 核心源代码
    ├── Analysis/                  # 分析服务
    │   └── ColonyAnalyzer.cs      # 殖民地分析器
    ├── Architecture/              # 架构核心
    │   ├── Events.cs              # 事件定义
    │   ├── Models.cs              # 基础数据模型
    │   ├── ServiceContainer.cs    # 依赖注入容器 + CoreServices门面
    │   ├── Interfaces/            # 接口定义
    │   │   ├── ICoreInterfaces.cs # 核心接口
    │   │   ├── IHistoryService.cs # 历史服务接口 [新增]
    │   │   ├── IPromptFactoryService.cs # 提示词工厂接口 [新增]
    │   │   ├── ISafeAccessService.cs # 安全访问接口 [新增]
    │   │   └── IPersistenceInterfaces.cs # 持久化接口
    │   └── Models/                # 数据模型
    │       ├── ConversationModels.cs # 对话模型 [新增]
    │       └── PromptModels.cs    # 提示词模型 [新增]
    ├── Commands/                  # 命令处理
    ├── Core/                      # 核心组件
    │   ├── LogFilter.cs           # 日志过滤器
    │   └── RimAICoreGameComponent.cs # 游戏组件
    ├── Officers/                  # AI官员系统
    │   ├── Base/
    │   │   └── OfficerBase.cs     # 官员基类
    │   ├── Events/
    │   │   ├── GovernorAdviceEvent.cs # 总督建议事件
    │   │   └── GovernorEventListener.cs # 事件监听器
    │   └── Governor.cs            # 总督AI官员
    ├── Prompts/
    │   └── PromptBuilder.cs       # 传统提示词构建器
    ├── RimAICoreMod.cs           # 模组主类
    ├── Services/                  # 服务实现
    │   ├── CacheService.cs        # 缓存服务
    │   ├── EventBusService.cs     # 事件总线服务
    │   ├── HistoryService.cs      # 对话历史服务 [新增]
    │   ├── LLMService.cs          # LLM调用服务
    │   ├── PersistenceService.cs  # 持久化服务
    │   ├── PromptFactoryService.cs # 提示词工厂服务 [新增]
    │   ├── SafeAccessService.cs   # 安全访问服务 [新增]
    │   └── Examples/
    │       └── GovernorPerformanceDemonstrator.cs # 性能演示
    ├── Settings/                  # 设置系统
    │   ├── CoreSettings.cs        # 核心设置数据
    │   └── CoreSettingsWindow.cs  # 设置窗口
    ├── UI/
    │   └── Dialog_OfficerSettings.cs # 官员设置对话框
    └── WIFE/                      # W.I.F.E. 系统（预留）
```

## 🚀 快速开始

### 先决条件

- Visual Studio 2019+ 或 JetBrains Rider
- .NET Framework 4.8 SDK
- RimWorld 1.6+ 游戏本体
- [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework) (依赖项)

### 克隆和构建

```bash
# 克隆仓库
git clone https://github.com/oidahdsah0/Rimworld_AI_Core.git
cd Rimworld_AI_Core

# 还原NuGet包
dotnet restore RimAI.Core/RimAI.Core.csproj

# 构建项目 (Debug)
dotnet build RimAI.Core/RimAI.Core.csproj --configuration Debug

# 构建项目 (Release)
dotnet build RimAI.Core/RimAI.Core.csproj --configuration Release
```

### 开发环境设置

1. **设置RimWorld路径**
   ```xml
   <!-- 在 RimAI.Core.csproj 中更新路径 -->
   <RimWorldPath>C:\Program Files (x86)\Steam\steamapps\common\RimWorld</RimWorldPath>
   ```

2. **配置调试启动**
   - 启动程序：`[RimWorldPath]\RimWorldWin64.exe`
   - 工作目录：`[RimWorldPath]`
   - 命令行参数：`-dev -logverbose`

3. **模组加载顺序**
   ```
   1. RimAI Framework (必须在前)
   2. RimAI Core (本项目)
   3. 其他依赖此框架的模组
   ```

## 🏗️ 架构核心组件

### 新增核心服务

| 服务 | 文件位置 | 接口定义 | 职责描述 |
|------|----------|----------|----------|
| **HistoryService** | `Services/HistoryService.cs` | `IHistoryService.cs` | 管理多参与者对话历史，支持主线/附加历史分层检索 |
| **PromptFactoryService** | `Services/PromptFactoryService.cs` | `IPromptFactoryService.cs` | 智能组装结构化提示词，消费历史服务数据 |
| **SafeAccessService** | `Services/SafeAccessService.cs` | `ISafeAccessService.cs` | 提供RimWorld API的并发安全访问，自动重试机制 |

### 架构迁移重点

**从静态单例 → 依赖注入**
- 所有服务通过 `ServiceContainer` 注册和管理
- `CoreServices` 门面提供统一访问入口
- 废弃旧的 `.Instance` 静态属性模式

**玩家身份处理**
- `CoreServices.PlayerStableId`：用于数据关联，永不改变
- `CoreServices.PlayerDisplayName`：用于UI显示，用户可修改

## 🔧 开发工作流

### 1. 添加新服务

```bash
# 1. 在 Architecture/Interfaces/ 中定义接口
touch RimAI.Core/Source/Architecture/Interfaces/IMyNewService.cs

# 2. 在 Services/ 中实现服务
touch RimAI.Core/Source/Services/MyNewService.cs

# 3. 在 ServiceContainer.cs 中注册
# 4. 在 CoreServices 中添加访问器
# 5. 编写单元测试
```

### 2. 创建新AI官员

```bash
# 1. 继承 OfficerBase
touch RimAI.Core/Source/Officers/MyNewOfficer.cs

# 2. 实现抽象方法
# 3. 在 ServiceContainer 中注册
# 4. 添加UI集成
```

### 3. 调试流程

```bash
# 构建并启动调试
dotnet build RimAI.Core/RimAI.Core.csproj --configuration Debug
# 启动 RimWorld 进行调试

# 查看日志
# Windows: %USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Player.log
# 关键日志前缀: [RimAI.Core], [ServiceContainer], [HistoryService], [PromptFactory]
```

## 🧪 测试和质量保证

### 单元测试结构

```
Tests/
├── Architecture/
│   ├── ServiceContainerTests.cs
│   └── CoreServicesTests.cs
├── Services/
│   ├── HistoryServiceTests.cs
│   ├── PromptFactoryServiceTests.cs
│   └── SafeAccessServiceTests.cs
└── Officers/
    └── GovernorTests.cs
```

### 性能测试

使用 `GovernorPerformanceDemonstrator.cs` 进行性能基准测试：

```csharp
// 在游戏中执行性能测试
await GovernorPerformanceDemonstrator.RunDemonstration();
```

### 代码质量检查

```bash
# 代码格式化
dotnet format RimAI.Core/RimAI.Core.csproj

# 静态分析 (如果配置了)
dotnet analyze RimAI.Core/RimAI.Core.csproj
```

## 📦 构建和发布

### 构建配置

```xml
<!-- Debug 配置 -->
<Configuration>Debug</Configuration>
<DebugSymbols>true</DebugSymbols>
<DebugType>full</DebugType>
<Optimize>false</Optimize>

<!-- Release 配置 -->
<Configuration>Release</Configuration>
<DebugSymbols>false</DebugSymbols>
<DebugType>none</DebugType>
<Optimize>true</Optimize>
```

### 发布流程

```bash
# 1. 版本号更新
# 更新 About/About.xml 中的版本号

# 2. 构建 Release 版本
dotnet build RimAI.Core/RimAI.Core.csproj --configuration Release

# 3. 运行所有测试
dotnet test

# 4. 创建发布包
# 将 RimAI.Core/ 目录打包（排除 obj/, bin/, .vs/ 等）

# 5. 更新文档
# 确保 CHANGELOG.md 包含最新更改
```

## 🔍 故障排除

### 常见构建错误

**错误：找不到RimWorld引用**
```bash
# 解决：更新项目文件中的RimWorld路径
# <Reference Include="Assembly-CSharp">
#   <HintPath>$(RimWorldPath)\RimWorldWin64_Data\Managed\Assembly-CSharp.dll</HintPath>
# </Reference>
```

**错误：服务未注册**
```bash
# 解决：检查 ServiceContainer.RegisterDefaultServices() 方法
# 确保新服务已正确注册
```

### 调试技巧

```csharp
// 使用服务状态诊断
Log.Message(CoreServices.GetServiceStatusReport());

// 检查特定服务
if (CoreServices.History == null)
{
    Log.Error("HistoryService not registered!");
}

// 性能监控
var cacheStats = CoreServices.CacheService.GetStats();
Log.Message($"Cache performance: {cacheStats.TotalAccessCount} accesses");
```

## 📋 贡献指南

### 代码风格

- 使用 C# 命名约定（PascalCase for public, camelCase for private）
- 异步方法添加 `Async` 后缀
- 接口以 `I` 开头
- 私有字段以 `_` 开头

### 提交规范

```bash
# 功能添加
git commit -m "feat: add new HistoryService for conversation management"

# 错误修复  
git commit -m "fix: resolve ServiceContainer circular dependency issue"

# 文档更新
git commit -m "docs: update API reference for new services"

# 重构
git commit -m "refactor: migrate from static singletons to dependency injection"
```

### Pull Request 检查清单

- [ ] 代码遵循项目风格指南
- [ ] 添加了适当的单元测试
- [ ] 更新了相关文档
- [ ] 所有现有测试通过
- [ ] 在游戏中测试了新功能
- [ ] 更新了 CHANGELOG.md

## 📞 联系方式

- **GitHub Issues**: 报告 Bug 和功能请求
- **Discussions**: 技术讨论和架构决策
- **Discord**: [RimAI 社区服务器](链接待补充)

## 📄 许可证

本项目采用 [MIT License](../LICENSE) 开源协议。

---

*🛠️ 感谢您为 RimAI Core 框架的贡献！每一行代码都让 RimWorld 的AI变得更加智能。*
