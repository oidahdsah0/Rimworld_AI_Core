# 🏗️ RimAI Core - Enterprise-Grade AI Framework

[English](README.md) | [简体中文](README_zh-CN.md) | [Documentation](docs/)

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![RimWorld](https://img.shields.io/badge/RimWorld-1.6-brightgreen.svg)](https://rimworldgame.com/)
[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-blue.svg)](https://dotnet.microsoft.com/download/dotnet-framework)
[![Architecture](https://img.shields.io/badge/Architecture-Enterprise-red.svg)](docs/ARCHITECTURE.md)

> **The first enterprise-grade AI framework for RimWorld featuring SOLID principles, dependency injection, event-driven architecture, and production-ready infrastructure for intelligent colony management.**

**🏢 Enterprise Architecture** • **🧠 Advanced AI Integration** • **🔧 Production-Ready Infrastructure**

**Author**: [@oidahdsah0](https://github.com/oidahdsah0)  
**Created**: 16 July 2025  
**Dependencies**: [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework)

---

## 🧠 **Core Philosophy: Dual-Layer Architecture**

RimWorld AI Core is built on a revolutionary **dual-layer architecture** that separates intelligence from execution:

### **Layer 1: LLM Layer - Material Depth System**
- **Level 1**: Basic Status (基础状态)
- **Level 2**: Detailed Analysis (详细分析)  
- **Level 3**: Deep Insight (深度洞察)
- **Level 4**: Predictive Modeling (预测建模)
- **Level 5**: Strategic Planning (战略规划)

### **Layer 2: Execution Layer - Action System**
- **Query**: Information retrieval and analysis
- **Suggest**: Recommendations without execution
- **Execute**: Direct game world actions
- **Monitor**: Continuous monitoring and feedback

---

## 🎯 **Key Features**

### **Three-Officer System**
- **🏛️ Governor**: Overall colony management and strategic decisions
- **⚔️ Military Officer**: Combat strategy and defense planning
- **📦 Logistics Officer**: Resource management and production optimization

### **Direct Command Interface**
- Natural language command processing
- Context-aware instruction interpretation
- Real-time feedback and status updates

### **W.I.F.E. System**
- **W**arden's **I**ntegrated **F**oresight **E**ngine
- Three AI personalities: MELCHIOR-1, BALTHASAR-2, CASPER-3（EVA-MAGI-EXTENTION）
- Deep narrative integration with emotional intelligence
- Memory synchronization and personality evolution
- Resurrection questline and philosophical depth
---

## 🚀 **Getting Started**

### **Prerequisites**
- RimWorld 1.6 or later
- [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework) (Required dependency)
- API access to a supported LLM service (OpenAI, DeepSeek, Ollama, etc.)

### **Installation**
⚠️ **CRITICAL: You MUST strictly follow the setup instructions on the RimAI Framework Mod page, or AI will NOT work!**
⚠️ **CRITICAL: You MUST strictly follow the setup instructions on the RimAI Framework Mod page, or AI will NOT work!**
⚠️ **CRITICAL: You MUST strictly follow the setup instructions on the RimAI Framework Mod page, or AI will NOT work!**

1. Install [RimAI Framework](https://github.com/oidahdsah0/Rim_AI_Framework) first
2. Download RimWorld AI Core from [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD) or [GitHub Releases](https://github.com/oidahdsah0/Rimworld_AI_Core/releases)
3. Configure your LLM API settings in the Framework mod settings
4. Launch RimWorld and enjoy intelligent colony management!

---

## 📐 **Enterprise Architecture Overview**

### **Core Architecture Patterns**
- **🏗️ Dependency Injection Container**: ServiceContainer manages service lifecycles
- **🚌 Event-Driven Architecture**: EventBus enables decoupled component communication
- **🛡️ Safe Access Layer**: SafeAccessService solves RimWorld API concurrent access issues
- **💾 Intelligent Caching System**: CacheService provides multi-tier caching and performance optimization
- **🔄 Async Programming Model**: Full-stack async/await pattern ensures UI responsiveness

### **System Architecture Diagram**
```
🏗️ RimAI Core - Enterprise Architecture
├── 🧠 LLM Layer (Intelligence)
│   ├── 📝 Prompt Engineering Service
│   ├── 🏗️ Context Building Service
│   ├── 🔍 Response Parsing Service
│   └── 📊 Material Depth Analysis
├── ⚡ Execution Layer (Action System)
│   ├── 🎯 Command Interpreter
│   ├── 👁️ Game State Monitor
│   ├── 🚀 Action Executor
│   └── 📡 Feedback Generator
├── 🏢 Infrastructure Layer (Enterprise)
│   ├── 🗃️ Service Container (DI)
│   ├── 🚌 Event Bus
│   ├── 🛡️ Safe Access Service
│   └── 💾 Cache Management
└── 🎨 User Interface
    ├── 👨‍💼 Officer Control Panels
    ├── 💻 Command Terminal
    └── 📊 Real-time Status Display
```

---

## 🔧 **Enterprise Technology Stack**

### **Core Technical Features**
- **🏗️ SOLID Principles**: Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion
- **🔄 Async Programming**: Comprehensive async/await patterns preventing UI blocking
- **🛡️ Exception Safety**: Unified exception handling, retry mechanisms, circuit breaker patterns
- **📊 Performance Monitoring**: Built-in metrics collection, cache statistics, execution time tracking
- **🔧 Extensible Architecture**: Plugin-based design, hot-swappable components, modular development

### **Infrastructure Services**
| Service | Description | Enterprise Features |
|---------|-------------|-------------------|
| **ServiceContainer** | Dependency Injection Container | Lifecycle management, circular dependency detection |
| **EventBus** | Event-driven communication | Async publishing, subscription management, error isolation |
| **SafeAccessService** | API Safe Access | Concurrency protection, auto-retry, failure recovery |
| **CacheService** | Intelligent Caching | Multi-tier cache, TTL management, memory optimization |
| **LLMService** | AI Model Integration | Connection pooling, request throttling, failover |

### **Supported LLM Services**
- **🤖 OpenAI**: GPT-4, GPT-3.5-turbo (Production recommended)
- **🧠 DeepSeek**: deepseek-chat (Cost-optimized choice)
- **🏠 Ollama**: Local model deployment (Privacy-first)
- **⚡ vLLM**: High-performance inference server
- **🔌 Compatible APIs**: Any OpenAI-format API service

### **Deployment Requirements**
- **Runtime**: .NET Framework 4.7.2 or higher
- **Game Version**: RimWorld 1.6+ (backward compatible)
- **Network**: Internet connection (cloud LLMs) or local deployment (Ollama/vLLM)
- **Memory**: Recommended 8GB+ RAM (large colonies + AI processing)
- **Storage**: ~50MB disk space (framework + cache)

---

## 🤝 **Contributing**

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### **Development Setup**
1. Clone this repository
2. Ensure you have the RimAI Framework project available
3. Open the solution in Visual Studio or your preferred IDE
4. Build and test your changes

---

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 🙏 **Acknowledgments**

- The RimWorld modding community for their incredible support
- Ludeon Studios for creating RimWorld
- The AI/ML community for advancing language model technology

---

## 📞 **Support**

- **Issues**: [GitHub Issues](https://github.com/oidahdsah0/Rimworld_AI_Core/issues)
- **Discussions**: [GitHub Discussions](https://github.com/oidahdsah0/Rimworld_AI_Core/discussions)
- **Steam Workshop**: [Workshop Page](https://steamcommunity.com/sharedfiles/filedetails/?id=TBD)

---

*Built with ❤️ for the RimWorld community*
