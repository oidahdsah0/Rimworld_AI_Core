# RimWorld AI Core

[English](README.md) | [简体中文](README_zh-CN.md) | [文档](docs/)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](https://rimworldgame.com/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)

> **一个全面的AI驱动的RimWorld殖民地管理系统，具有智能决策、双层架构和高级工作流程优化功能。**

**作者**: [@oidahdsah0](https://github.com/oidahdsah0)  
**创建时间**: 2025年7月16日  
**依赖项**: [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)

---

## 🧠 **核心理念：双层架构**

RimWorld AI Core 建立在革命性的**双层架构**之上，将智能与执行分离：

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
- **W**orkflow **I**ntelligence **F**or **E**fficiency (工作流智能效率系统)
- 自动化任务优先级排序
- 智能资源分配
- 性能优化建议

---

## 🚀 **开始使用**

### **先决条件**
- RimWorld 1.6 或更高版本
- [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework) (必需依赖项)
- 支持的LLM服务的API访问权限 (OpenAI、DeepSeek、Ollama等)

### **安装**
1. 首先安装 [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)
2. 从 [Steam创意工坊](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD) 或 [GitHub Releases](https://github.com/oidahdsah0/Rimworld_AI_Core/releases) 下载 RimWorld AI Core
3. 在Framework模组设置中配置您的LLM API设置
4. 启动RimWorld并享受智能殖民地管理！

---

## 📐 **架构概览**

```
RimWorld AI Core
├── LLM层 (智能)
│   ├── 提示词工程
│   ├── 上下文构建
│   ├── 响应解析
│   └── 素材深度分析
├── 执行层 (动作)
│   ├── 命令解释器
│   ├── 游戏状态监视器
│   ├── 动作执行器
│   └── 反馈生成器
└── 用户界面
    ├── 官员面板
    ├── 命令终端
    └── 状态显示
```

---

## 🔧 **技术细节**

### **支持的LLM服务**
- OpenAI (GPT-4, GPT-3.5)
- DeepSeek (deepseek-chat)
- Ollama (本地部署)
- vLLM (本地部署)
- 任何OpenAI兼容的API

### **系统要求**
- .NET Framework 4.7.2
- RimWorld 1.6+
- 互联网连接 (用于云端LLM服务)

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
- **Steam创意工坊**: [工坊页面](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD)

---

*用 ❤️ 为 RimWorld 社区构建*
