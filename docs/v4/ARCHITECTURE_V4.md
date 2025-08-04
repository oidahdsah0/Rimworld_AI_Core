# RimAI.Core V4 架构文档（Draft）

> 版本：v4.0.0-alpha  
> 状态：草案（Phase 0 – Skeleton）

## 1. 目标

V4 旨在将 v3 的宏大设计拆分为多个「可运行、可验证、可回滚」的小步增量。每完成一个阶段即可在游戏内通过 Debug 面板进行验证，最大程度降低调试成本。

## 2. 拆解原则

1. **最小可运行单元（MVP）**：每阶段代码均可编译并加载到 RimWorld，不出现红色报错。  
2. **垂直切片**：优先实现端到端闭环，而不是横向一次铺满全部服务。  
3. **接口冻结**：阶段内已发布的 Contracts 不得破坏性修改；必要变更通过 `vNext` 后缀预留。  
4. **文档先行**：每阶段开始前必须更新 `docs/v4/phases` 内相应文件。  
5. **可调试**：Debug 面板提供一键测试按钮，保证非开发者也能复现问题。

## 3. 分层视图（简）

| 层级 | 主要职责 | 对应文件夹 |
|------|----------|-----------|
| UI | 游戏窗口、调试面板 | `Source/UI` |
| Modules | 领域功能模块（LLM、Tooling 等） | `Source/Modules/*` |
| Infrastructure | DI、缓存、调度等通用基建 | `Source/Infrastructure` |
| Contracts | 稳定接口、DTO、事件 | `Source/Contracts` |
| Lifecycle | RimAIMod 入口、GameComponent | `Source/Lifecycle` |

> 详细依赖请见 `ARCHITECTURE_V4_DIAGRAM.md`。

## 4. 模块拆分

| 模块 | 首次出现阶段 | 说明 |
|------|-------------|------|
| LLM | P2 | 负责与 RimAIApi 通信，封装缓存/重试 |
| WorldAccess | P3 | ISchedulerService + IWorldDataService |
| Tooling | P4 | 工具注册与执行 |
| Orchestration | P5 | 五步工作流的大脑 |
| Persistence | P6 | IPersistenceService & 存档生命周期 |
| Eventing | P7 | 事件总线 + 聚合器 |
| Persona | P8 | 人格管理与系统提示词 |

## 5. 调试面板规范

* 文件：`Source/UI/DebugPanel/MainTabWindow_RimAIDebug.cs`  
* 默认隐藏，可在 Mod 设置中勾选“开发者模式”显示。  
* 左侧按钮列表按阶段分组；右侧多行文本框实时输出日志。  
* 所有按钮调用 `CoreServices`，避免构造函数注入的限制。

---

这个文档会保持随着阶段更新而迭代。
