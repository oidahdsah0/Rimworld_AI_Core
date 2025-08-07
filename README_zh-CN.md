# 🏗️ RimAI Core - V4 架构

[English](README.md) | [简体中文](README_zh-CN.md)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](https://rimworldgame.com/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Status](https://img.shields.io/badge/Status-v4.1.1-orange.svg)](https://steamcommunity.com/sharedfiles/filedetails/?id=3529186453)

> **RimAI Core v4：一个为 RimWorld 设计的、增量式的企业级 AI 框架，为快速迭代、可验证的进度和无与伦比的可扩展性而重构。V4 继承了 v3 强大的设计哲学，同时采用分阶段交付模型，以确保稳定性和持续改进。**

**✨ V4 版本亮点:**
- **分阶段交付**: 从 P0 到 P8 的增量式开发，确保每个阶段都交付一个可验证的、功能性的最小可行产品（MVP）。
- **模块化架构**: 清晰定义的模块（LLM、WorldAccess、Tooling、Orchestration 等），实现高内聚和低耦合。
- **稳定的合约层**: 一个专用的 `Contracts` 程序集，提供稳定、版本化的 API，让第三方 Mod 可以放心集成。
- **健壮的基础设施**: 构建于依赖注入、集中式配置服务和弹性调度器的基础之上。
- **智能编排**: 一个五步走的、工具辅助的查询工作流，支持复杂的、多步骤的 AI 交互。

**作者**: [@oidahdsah0](https://github.com/oidahdsah0)  
**创建时间**: 2025年7月16日  
**依赖项**: [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)

---

## 🚀 快速上手

### 前置要求
- RimWorld 1.6 或更高版本
- Harmony
- [RimAI.Framework](https://github.com/oidahdsah0/Rim_AI_Framework) (严格要求 v4.1+ 版本)
- 可访问受支持的 LLM 服务（如 OpenAI, DeepSeek, Ollama 等）的 API 密钥

### 安装指南
⚠️ **至关重要：您必须严格遵循 RimAI Framework Mod 页面的设置说明，否则 AI 将无法工作！**

1.  **首先安装 [RimAI.Framework](https://github.com/oidahdsah0/Rim_AI_Framework)。**
2.  从 [Steam 创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD) 或 [GitHub Releases](https://github.com/oidahdsah0/Rimworld_AI_Core/releases) 下载 RimAI Core。
3.  在 Framework 的 Mod 设置菜单中配置您的 LLM API 密钥。
4.  启动 RimWorld 并通过 `Debug Panel`（调试面板）来测试 V4 的各项功能。

---

## 📐 V4 架构概览

V4 架构旨在实现清晰性、可测试性和增量开发。它将不同的关注点分离到不同的层和模块中，确保系统既强大又易于维护。

### V4 架构图

```mermaid
graph TB

    subgraph "UI 层"
        DebugPanel["调试面板"]
        GameWindows["助手 / 对话窗口"]
    end

    subgraph ModulesLayer ["模块层"]
        Orchestration["模块: 智能编排"]
        Tooling["模块: 工具系统"]
        LLM["模块: LLM 网关"]
        WorldAccess["模块: 世界访问"]
        Persistence["模块: 持久化"]
        Eventing["模块: 事件处理"]
        Persona["模块: AI 人格"]
    end

    subgraph InfrastructureLayer ["基础设施层"]
        ServiceContainer["DI 容器"]
        Scheduler["调度服务"]
        Cache["缓存服务"]
        Config["配置服务"]
    end
    
    subgraph "合约层 (稳定 API)"
        Contracts["接口 & 数据传输对象"]
    end

    subgraph "外部依赖"
        RimWorld["RimWorld 游戏引擎"]
        Framework["RimAI.Framework API"]
    end

    %% 流程
    GameWindows --> Orchestration
    Orchestration --> LLM
    Orchestration --> Tooling
    Tooling --> WorldAccess
    Tooling --> Contracts
    WorldAccess --> Scheduler
    Scheduler --> RimWorld
    LLM --> Cache
    LLM --> Framework
    
    ModulesLayer --> ServiceContainer
    ModulesLayer --> Contracts
    InfrastructureLayer --> Contracts
```

### 核心服务与模块

| 服务 / 模块 | 角色与职责 | 关键特性 |
| :--- | :--- | :--- |
| **`ServiceContainer`** | 管理服务生命周期和依赖关系。 | 通过构造函数注入强制执行 SOLID 原则。 |
| **`ILLMService`** | 所有 LLM 通信的唯一网关。 | 集中处理缓存、重试和熔断机制。 |
| **`IWorldDataService`** | 用于安全访问主线程游戏数据的防腐层。 | 使用 `ISchedulerService` 来防止线程问题。 |
| **`IToolRegistryService`**| 发现并管理一个 AI 能力的“工具箱”。 | 无需修改核心代码即可动态扩展 AI 技能。 |
| **`IOrchestrationService`**| 指挥五步工作流的“大脑”。 | 集成工具和数据以生成智能回复。 |
| **`IPersistenceService`** | 管理 AI 状态的保存和加载。 | 将业务逻辑与 RimWorld 的 `Scribe` 系统解耦。 |
| **`IPersonaService`** | 管理 AI 人格及其系统提示词。 | 将“AI 是谁”与“AI 能做什么”分离。 |

---

## 🔧 技术与原则

- **SOLID 原则**: 整个架构构建在 SOLID 设计原则的基础之上。
- **全栈异步**: 全栈的 async/await 模型确保了一个响应迅速、无阻塞的 UI。
- **弹性设计**: 统一的异常体系、重试策略和熔断器确保了系统的健壮性。
- **可测试性**: `Debug Panel` 为每个开发阶段提供脚本化的测试用例，确保端到端的功能。
- **可扩展性**: `IRimAITool` 接口和 `IToolRegistryService` 允许轻松添加新的 AI 能力。

---

## 🤝 参与贡献

我们欢迎各种形式的贡献！请参阅我们（即将发布）的 `CONTRIBUTING.md` 文件，了解开发流程、编码标准和拉取请求指南的详细信息。

### 开发路线图
V4 的实现被分解为 8 个阶段。您可以在 [V4 实施计划](docs/v4/IMPLEMENTATION_V4.md) 中跟踪项目进度。

| 阶段 | 里程碑 | 状态 |
| :--- | :--- | :--- |
| **P0** | 骨架 & DI | ✅ 已完成 |
| **P1** | DI & 配置 | ✅ 已完成 |
| **P2** | LLM 网关 | ✅ 已完成 |
| **P3** | 调度器 & 世界数据 | ✅ 已完成 |
| **P4** | 工具系统 | ✅ 已完成 |
| **P5** | 智能编排 | ✅ 已完成 |
| **P6** | 持久化 | ✅ 已完成 |
| **P7** | 事件聚合器 | ⏳ 待定 |
| **P8** | 人格 & UI | ⏳ 待定 |

---

## 📄 许可证

本项目采用 MIT 许可证 - 详情请参阅 [LICENSE](LICENSE) 文件。

---

## 📞 支持

- **问题反馈**: [GitHub Issues](https://github.com/oidahdsah0/Rimworld_AI_Core/issues)
- **参与讨论**: [GitHub Discussions](https://github.com/oidahdsah0/Rimworld_AI_Core/discussions)
- **创意工坊**: [Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD)
