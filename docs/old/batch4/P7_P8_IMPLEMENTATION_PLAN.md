# RimAI.Core V4 – P7~P8 详细实施方案 (修订版)

> 版本：v4.0.0 正式版预备  
> 更新日期：[当前日期]  
> 范围：Event Aggregator (P7)、Dynamic Persona & Streaming UI (P8)  
> **核心修订**：P8 的 Persona 系统已根据最新讨论，从静态模板加载模式，全面升级为支持玩家动态创建、修改、删除并可随游戏存档持久化的新架构。

---

## 1. 背景与目标

P7 的目标不变：通过 **事件聚合器** 解决游戏内高频事件对 LLM API 的冲击问题，实现智能节流。

P8 的目标已**重大升级**：我们将构建一个**动态的、可持久化的人格(Persona)系统**。玩家将不再使用预设模板，而是可以像管理装备一样，在游戏中自由地**创建、编辑和删除**AI的人格。这些人格将与游戏存档绑定，成为玩家独一无二的AI伙伴。同时，我们将完成流式UI的改造，提供即时、流畅的对话体验。

**核心交付：**
1. **P7 – IEventAggregatorService** (同前)
   * 实现智能节流与优先级处理。
2. **P8 – 动态 Persona 系统 & 流式 UI**
   * `IPersonaService` 支持对 `Persona` 对象的完整 CRUD 操作。
   * `Persona` 数据能够通过 `IPersistenceService` 被完整地写入游戏存档，并在读档时恢复。
   * 一个专用的UI界面，用于管理所有自定义人格。
   * 主聊天UI支持从新系统中动态选择人格，并以流式渲染回复。

---

## 2. 里程碑与时间预估

| 阶段 | 目标 MVP | Debug 面板/UI | 预计工时 |
|------|----------|---------------|----------|
| **P7** | 高频事件节流 | Trigger Test Events | 2 天 |
| **P8** | 动态人格CRUD与持久化 | **Persona管理窗口** | 5 天 |
| **合计** | — | — | **7 天** |

> P8 的工作量因架构升级而增加。

---

## 3. Gate 验收清单

| 阶段 | Gate 条件（全部满足才可合并主干） |
|------|-----------------------------------|
| **P7** | (同前) ① DebugPanel **Trigger Test Events** 连续点击5次，仅触发 1 次 LLM 调用 ② 冷却窗口生效 ③ 聚合提示词按优先级排序 ④ 单元测试通过。 |
| **P8** | ① **CRUD:** 在 Persona 管理窗口中，可以成功创建、读取、更新和删除一个人格，且名称查重逻辑生效 ② **持久化:** 创建的人格在存档、退出并重新读档后依然存在且可被选用 ③ **集成:** 在主聊天窗口选用自定义人格后，AI的回复风格与设定一致 ④ **流式UI:** 回复文本以打字机效果逐字显示 ⑤ **性能:** 流式渲染每帧新增耗时 ≤ 0.5 ms，Profiler 必须验证通过 ⑥ **单元测试:** 覆盖 CRUD、持久化与性能回归逻辑。 |

---

## 4. 阶段交付一览

| 阶段 | 关键类 / 接口 | 新增/修改文件 | UI 变更 |
|------|---------------|--------------|-----------|
| P7 | (同前) `IEvent`, `IEventBus`, `IEventAggregatorService` | `Modules/Eventing/*` | DebugPanel: +Trigger Test Events |
| P8 | `IPersonaService` (CRUD接口), `Persona` (DTO) | `Contracts/Persona/*` | |
| P8 | `PersonaService` (CRUD实现) | `Modules/Persona/*` | |
| P8 | `IPersistenceService` | `Infrastructure/Persistence/IPersistenceService.cs` | |
| P8 | `MainTabWindow_PersonaManager` | `UI/PersonaManager/` | **新增:** 人格管理窗口 |
| P8 | `MainTabWindow_RimAIDebug` | `UI/DebugPanel/*` | DebugPanel: +按钮打开Persona管理窗口 |

---

## 5. 详细任务拆解

### 5.1 P7 Event Aggregator (无变更)

| # | 任务 | 预计时长 |
|---|------|----------|
| 7-1 | 定义 `IEvent`, `IEventBus` 接口 (Contracts) | 0.2d |
| 7-2 | 实现 `EventBus` | 0.4d |
| 7-3 | 实现 `EventAggregatorService` | 1.0d |
| 7-4 | 单元测试 | 0.4d |
| 7-5 | DebugPanel 按钮 | 0.2d |

### 5.2 P8 Dynamic Persona & Streaming UI (新)

| # | 任务 | 预计时长 |
|---|------|----------|
| 8-1 | (契约) 重构 `IPersonaService` 接口，包含`Get`, `GetAll`, `Add`, `Update`, `Delete`等方法。定义 `Persona` DTO。 | 0.5d |
| 8-2 | (实现) 实现 `PersonaService` 的内存中 CRUD 逻辑，使用 `Dictionary<string, Persona>` 作为主存储。 | 1.0d |
| 8-3 | (持久化) 在 `IPersistenceService` 中增加 `PersistPersonas(PersonaService)` 方法。修改 `PersonaService` 提供 `GetState/LoadState` 方法。 | 0.8d |
| 8-4 | (集成) 修改 `OrchestrationService`，使其从 `PersonaService` 获取人格，而非静态模板。 | 0.2d |
| 8-5 | (UI) 设计并实现 `MainTabWindow_PersonaManager` 窗口，包含人格列表、名称和系统提示输入框、保存/删除按钮。 | 1.0d |
| 8-6 | (测试) 编写单元测试，覆盖 `PersonaService` CRUD 与持久化交互。 | 0.5d |
|| 8-7 | (校验) 实现 Persona 数据完整性校验与自动降级（缺失人格 → 默认 Persona）。 | 0.3d |
|| 8-8 | (文档 & CI) 更新 `ARCHITECTURE_V4.md`、`CHANGELOG.md`，通过文档签名 CI。 | 0.2d |
|| 8-9 | (性能优化) 流式 UI 字符批量追加，GC 优化 & Profiler 验证。 | 0.3d |

---

## 6. TODO Checklist

### P7 Event Aggregator ⬜
- (同前)

### P8 Dynamic Persona & Streaming UI (新) ⬜
- [ ] (契约) `IPersonaService` (CRUD) 和 `Persona` DTO
- [ ] (实现) `PersonaService` 内存 CRUD
- [ ] (持久化) `IPersistenceService` 集成 + 更新 `PersistenceManager`
- [ ] (校验) Persona 数据完整性 & 自动降级
- [ ] (集成) `OrchestrationService` 对接
- [ ] (UI) `Persona` 管理窗口（重命名冲突提示、删除确认）
- [ ] (性能) 流式 UI ≤ 0.5 ms/帧，Profiler 验证
- [ ] (测试) CRUD / 持久化 / 性能回归单元测试
- [ ] (文档 & CI) 更新 ARCHITECTURE_V4.md / CHANGELOG.md 并通过签名
- [ ] 录像
