# RimAI.Core V4 – P7~P8 详细实施方案

> 版本：v4.0.0 正式版预备  
> 更新日期：[当前日期]  
> 范围：Event Aggregator (P7)、Persona & Streaming UI (P8)  
> 本文需与 `ARCHITECTURE_V4.md` / `IMPLEMENTATION_V4.md` 同步维护，任何接口或 Gate 变更务必同时修订。

---

## 1. 背景与目标

在 P6 完成后，我们的 AI 已经具备了完整的“思考-行动-记忆”闭环。P7 和 P8 是实现产品级体验的最后冲刺阶段。P7 的目标是通过 **事件聚合器** 解决游戏内高频事件（如战斗日志）对 LLM API 的冲击问题，实现智能节流；P8 的目标是引入 **AI 人格 (Persona)** 和 **流式UI渲染**，让 AI 的回复更具个性化，并大幅提升交互的响应速度和沉浸感。

**核心交付：**
1. **P7 – IEventAggregatorService**  
   * 实现一个带定时节流和优先级处理能力的事件处理模型。
   * 连续的低优先级事件将被聚合，只有高优先级事件或定时器触发时，才发起一次 LLM 请求。
2. **P8 – Persona & Streaming UI**  
   * `IPersonaService` 能够从 Defs 加载并管理人格模板。
   * `IOrchestrationService` 的行为受外部传入的 `Persona` 影响。
   * UI 层（如 Debug Panel）能够逐块、实时地渲染 `IOrchestrationService` 返回的流式响应。

---

## 2. 里程碑与时间预估

| 阶段 | 目标 MVP | Debug 面板按钮 | 预计工时 |
|------|----------|----------------|----------|
| **P7** | 高频事件节流 | Trigger Test Events | 2 天 |
| **P8** | 人格生效 + 流式渲染 | Chat with Persona | 3 天 |
| **合计** | — | — | **5 天** |

> 时间估算基于 2 人 Review + QA 录像；若接口或 RimWorld 版本变更需适时调整。

---

## 3. Gate 验收清单

| 阶段 | Gate 条件（全部满足才可合并主干） |
|------|-----------------------------------|
| **P7** | ① DebugPanel **Trigger Test Events** 按钮连续点击5次，内部日志显示事件被聚合，且仅触发 1 次 LLM 调用 ② 冷却窗口生效期间，新的聚合请求被拒绝 ③ 聚合的提示词内容按事件优先级降序排列 ④ 单元测试覆盖率 ≥ 85% (`EventAggregatorServiceTests.cs`) |
| **P8** | ① DebugPanel **Chat with Persona** 按钮选择不同 Persona（如“暴躁的战士” vs “友好的助手”），AI 回复的语气和风格有明显区别 ② UI 文本逐字流式输出，而非等待完整回复后一次性显示 ③ `IOrchestrationService` 的 `ExecuteToolAssistedQueryAsync` 接口接受 `personaContext` 参数并影响最终提示词 ④ 单元测试 `PersonaServiceTests` 和 `OrchestrationServicePersonaTests` 通过 |

---

## 4. 阶段交付一览

| 阶段 | 关键类 / 接口 | 新增/修改文件 | DebugPanel 变更 |
|------|---------------|--------------|----------------|
| P7 | `IEvent`, `EventPriority`, `IEventBus`, `IEventAggregatorService` | `Modules/Eventing/*` | + Trigger Test Events |
| P8 | `IPersonaService`, `Persona` | `Modules/Persona/*`, `Defs/PersonaDefs/*.xml` | + Chat with Persona |
| P8 | `IOrchestrationService` | `Contracts/IOrchestrationService.cs`, `Modules/Orchestration/OrchestrationService.cs` | 更新 Ask Colony Status 以支持 Persona |
| P8 | `MainTabWindow_RimAIDebug` | `UI/DebugPanel/MainTabWindow_RimAIDebug.cs` | 改造输出区域以支持流式渲染 |

---

## 5. 详细任务拆解

### 5.1 P7 Event Aggregator

| # | 任务 | 预计时长 |
|---|------|----------|
| 7-1 | 定义 `IEvent` 接口和 `EventPriority` 枚举 (Contracts) | 0.2d |
| 7-2 | 实现轻量级 `IEventBus` (发布/订阅) | 0.4d |
| 7-3 | 实现 `EventAggregatorService`：订阅事件、定时节流、优先级排序、批量请求 | 1.0d |
| 7-4 | 单元测试：
* `AggregatesLowPriorityEvents`
* `TriggersOnHighPriorityEvent`
* `RespectsCooldownWindow` | 0.4d |
| 7-5 | DebugPanel 按钮 **Trigger Test Events**：模拟触发多个测试事件并验证聚合逻辑 | 0.2d |

**完成判定**：按钮点击后，日志正确显示事件聚合和节流过程；最终 LLM 请求次数符合预期。

### 5.2 P8 Persona & Streaming UI

| # | 任务 | 预计时长 |
|---|------|----------|
| 8-1 | 定义 `Persona` DTO 和 `IPersonaService` 接口 (Contracts) | 0.3d |
| 8-2 | 实现 `PersonaService`：从 XML Defs 加载人格模板 | 0.6d |
| 8-3 | 修改 `IOrchestrationService` 接口，增加 `personaContext` 参数 | 0.2d |
| 8-4 | 修改 `OrchestrationService` 实现，将 `personaContext` 整合到提示词工厂中 | 0.5d |
| 8-5 | 修改 `ILLMService`，暴露 `StreamResponseAsync` 方法 | 0.3d |
| 8-6 | DebugPanel 改造：UI 输出区支持 `IAsyncEnumerable` 逐块刷新 | 0.6d |
| 8-7 | DebugPanel 添加 **Chat with Persona** 按钮和 Persona 选择下拉框 | 0.2d |
| 8-8 | 单元测试：`PersonaServiceTests` (加载测试)，`OrchestrationServicePersonaTests` (提示词注入测试) | 0.3d |

**完成判定**：使用不同 Persona 对话，AI 回复风格迥异；UI 文本可见明显的打字机效果。

---

## 6. Debug Panel 更新

1. **左侧按钮**：新增 `P7` & `P8` 分组；分别包含 **Trigger Test Events**、**Chat with Persona**。
2. **P8 功能区**：在 “Chat with Persona” 按钮旁添加一个下拉菜单，用于选择当前使用的 Persona 模板。
3. **日志区**：
   * P7 – 实时打印：`[Event] Received: ...`, `[Aggregator] Event added to buffer.`, `[Aggregator] Cooldown active, skipping.`, `[Aggregator] Triggering LLM call with N events.`
   * P8 – 流式输出将直接在主输出框体现，无需额外日志。

---

## 7. 验收与交付

1. **CI**：`dotnet test` + `dotnet format`; 文档签名检查。
2. **录像**：流程录像 `media/p7_p8_demo.mp4`：演示事件聚合节流 -> 使用不同 Persona 对话并展示流式输出。
3. **Tag**：合并时打 `core/v4.0.0`。
4. **文档**：同步更新 `IMPLEMENTATION_V4.md` 表格 Gate 状态与日期；CHANGELOG 追加 `v4.0.0` 发布说明。

---

## 8. TODO Checklist

### P7 Event Aggregator ⬜
- [ ] `IEvent`, `IEventBus` 接口 (Contracts)
- [ ] `EventAggregatorService` 实现
- [ ] 单元测试 3 项
- [ ] DebugPanel Trigger Test Events

### P8 Persona & Streaming UI ⬜
- [ ] `IPersonaService`, `Persona` 接口 (Contracts)
- [ ] `PersonaService` 实现 + Defs 加载
- [ ] `IOrchestrationService` 接口更新
- [ ] `ILLMService` 暴露流式方法
- [ ] DebugPanel UI 流式渲染改造
- [ ] DebugPanel Persona 选择功能
- [ ] 单元测试 2 项
- [ ] CHANGELOG & 录像

> 完成本文件中所有 ✅ 项目后，即标记阶段 Gate 为 **通过**，并在此文件「TODO Checklist」处打勾保存版本。

