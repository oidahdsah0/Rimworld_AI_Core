# 🏗️ RimAI Core - 企业级AI框架

[English](README.md) | [简体中文](README_zh-CN.md) | [文档](docs/)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](https://rimworldgame.com/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Architecture](https://img.shields.io/badge/Architecture-Enterprise-red.svg)](docs/ARCHITECTURE.md)

> **首个为RimWorld打造的企业级AI框架，采用SOLID原则、依赖注入、事件驱动架构，以及生产就绪的基础设施，实现智能殖民地管理。**

**🏢 企业级架构** • **🧠 高级AI集成** • **🔧 生产就绪基础设施**

**作者**: [@oidahdsah0](https://github.com/oidahdsah0)  
**创建时间**: 2025年7月16日  
**依赖项**: [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)

---

## 🧠 **核心理念：双层架构**

RimAI Core 建立在革命性的**双层架构**之上，将智能与执行分离：

### **第一层：LLM层 - 素材深度系统**
- **Level 1**: 基础状态
- **Level 2**: 详细分析
- **Level 3**: 深度洞察
- **Level 4**: 预测建模
- **Level 5**: 战略规划

### **第二层：执行层 - 动作系统**
- **Query**: 信息检索和分析
- **Suggest**: 建议但不执行
- **Execute**: 直接的游戏世界动作
- **Monitor**: 持续监控和反馈

---

## 🎯 **核心功能**

### **三官员系统**
- **🏛️ 总督**: 整体殖民地管理和战略决策
- **⚔️ 军事官**: 战斗策略和防御规划
- **📦 后勤官**: 资源管理和生产优化

### **直接指令界面**
- 自然语言命令处理
- 上下文感知的指令解释
- 实时反馈和状态更新

### **W.I.F.E. 系统**
- **W**arden's **I**ntegrated **F**oresight **E**ngine (典狱长综合预见引擎)
- 三个AI人格: MELCHIOR-1, BALTHASAR-2, CASPER-3（EVA-MAGI-EXTENTION）
- 深度叙事整合与情感智能
- 记忆同步和人格进化
- 复活任务线和哲学深度

---

## 🚀 **开始使用**

### **先决条件**
- RimWorld 1.6 或更高版本
- [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework) (必需依赖项)
- 支持的LLM服务的API访问权限 (OpenAI、DeepSeek、Ollama等)

### **安装**
⚠️ **重要：必须严格按照 RimAI Framework Mod 页面的说明进行设置，否则AI无法启用！**
⚠️ **重要：必须严格按照 RimAI Framework Mod 页面的说明进行设置，否则AI无法启用！**
⚠️ **重要：必须严格按照 RimAI Framework Mod 页面的说明进行设置，否则AI无法启用！**

1. 首先安装 [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)
2. 从 [Steam创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=3529310374) 或 [GitHub Releases](https://github.com/oidahdsah0/Rimworld_AI_Core/releases) 下载 RimAI Core
3. 在Framework模组设置中配置您的LLM API设置
4. 启动RimWorld并享受智能殖民地管理！

---

## 📐 **企业级架构概览**

### **核心架构模式**
- **🏗️ 依赖注入容器**: ServiceContainer 统一管理服务生命周期
- **🚌 事件驱动架构**: EventBus 实现组件间解耦通信
- **🛡️ 安全访问层**: SafeAccessService 解决RimWorld API并发访问问题
- **💾 智能缓存系统**: CacheService 提供多级缓存和性能优化
- **🔄 异步编程模型**: 全栈async/await模式，确保UI响应性

### **系统架构图**
```
🏗️ RimAI Core - 企业级架构
├── 🧠 LLM层 (智能决策)
│   ├── 📝 提示词工程服务
│   ├── 🏗️ 上下文构建服务
│   ├── 🔍 响应解析服务
│   └── 📊 素材深度分析
├── ⚡ 执行层 (动作系统)
│   ├── 🎯 命令解释器
│   ├── 👁️ 游戏状态监视器
│   ├── 🚀 动作执行器
│   └── 📡 反馈生成器
├── 🏢 基础设施层 (企业级)
│   ├── 🗃️ 服务容器 (DI)
│   ├── 🚌 事件总线
│   ├── 🛡️ 安全访问服务
│   └── 💾 缓存管理
└── 🎨 用户界面
    ├── 👨‍💼 官员控制面板
    ├── 💻 命令终端界面
    └── 📊 实时状态显示
```

---

## 🔧 **企业级技术栈**

### **核心技术特性**
- **🏗️ SOLID原则**: 单一职责、开闭原则、里氏替换、接口隔离、依赖倒置
- **🔄 异步编程**: 全面的async/await模式，避免UI阻塞
- **🛡️ 异常安全**: 统一异常处理、重试机制、断路器模式
- **📊 性能监控**: 内置指标收集、缓存统计、执行时间追踪
- **🔧 可扩展架构**: 插件化设计、热插拔组件、模块化开发

### **基础设施服务**
| 服务 | 功能描述 | 企业级特性 |
|------|----------|-----------|
| **ServiceContainer** | 依赖注入容器 | 生命周期管理、循环依赖检测 |
| **EventBus** | 事件驱动通信 | 异步发布、订阅管理、错误隔离 |
| **SafeAccessService** | API安全访问 | 并发保护、自动重试、失败恢复 |
| **CacheService** | 智能缓存 | 多级缓存、TTL管理、内存优化 |
| **LLMService** | AI模型集成 | 连接池、请求限流、故障转移 |

### **支持的LLM服务**
- **🤖 OpenAI**: GPT-4、GPT-3.5-turbo (生产环境推荐)
- **🧠 DeepSeek**: deepseek-chat (成本优化选择)
- **🏠 Ollama**: 本地模型部署 (隐私优先)
- **⚡ vLLM**: 高性能推理服务器
- **🔌 兼容API**: 任何OpenAI格式的API服务

### **部署要求**
- **运行时**: .NET Framework 4.7.2 或更高版本
- **游戏版本**: RimWorld 1.6+ (向后兼容)
- **网络**: 互联网连接 (云端LLM) 或本地部署 (Ollama/vLLM)
- **内存**: 推荐 8GB+ RAM (大型殖民地 + AI处理)
- **存储**: 约50MB磁盘空间 (框架 + 缓存)

---

## 🤝 **贡献**

我们欢迎贡献！请查看我们的[贡献指南](CONTRIBUTING.md)了解详情。

### **开发设置**
1. 克隆此仓库
2. 确保您有RimAI Framework项目可用
3. 在Visual Studio或您喜欢的IDE中打开解决方案
4. 构建和测试您的更改

---

## 📄 **许可证**

此项目根据MIT许可证授权 - 查看[LICENSE](LICENSE)文件了解详情。

---

## 🙏 **致谢**

- RimWorld模组社区的incredible支持
- Ludeon Studios创造了RimWorld
- AI/ML社区推进了语言模型技术

---

## 📞 **支持**

- **问题**: [GitHub Issues](https://github.com/oidahdsah0/Rimworld_AI_Core/issues)
- **讨论**: [GitHub Discussions](https://github.com/oidahdsah0/Rimworld_AI_Core/discussions)
- **Steam创意工坊**: [工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=3529310374)

---

*用 ❤️ 为 RimWorld 社区构建*
