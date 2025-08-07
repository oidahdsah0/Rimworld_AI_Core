# RimAI.Core V4 – P5~P6 详细实施方案

> 版本：v4.0.0-beta 预备  
> 更新日期：2025-08-06  
> 范围：Orchestration (P5)、Persistence (P6)  
> 本文需与 `ARCHITECTURE_V4.md` / `IMPLEMENTATION_V4.md` 同步维护，任何接口或 Gate 变更务必同时修订。

---

## 1. 背景与目标

在 P4 工具系统落地后，AI 已具备“安全读/写世界”的离散能力。P5 的重点是把这些底层能力通过 **五步编排工作流** 汇聚成一个统一入口 (`IOrchestrationService`)；随后 P6 解决 **对话历史持久化**，保证 AI 拥有连续记忆并保持存档兼容。

**核心交付：**
1. **P5 – IOrchestrationService (最小版)**  
   * 完整实现五步工作流（Function Calling 决策 → 工具执行 → 再提示 → 自然语言回复 → 记录历史）。  
   * 单轮问答（无多轮上下文）即可，通过 Debug 面板输入“殖民地概况？”获取总结文本。
2. **P6 – 持久化子系统**  
   * `IPersistenceService` + `PersistenceManager(GameComponent)` + `HistoryService` 状态序列化。  
   * 退出 → 读档后，之前的对话历史能被查询到。

---

## 2. 里程碑与时间预估

| 阶段 | 目标 MVP | Debug 面板按钮 | 预计工时 |
|------|----------|----------------|----------|
| **P5** | ExecuteToolAssistedQueryAsync 闭环 | Ask Colony Status | 3 天 |
| **P6** | History 持久化 & 恢复 | Record History | 2 天 |
| **合计** | — | — | **5 天** |

> 时间估算基于 2 人 Review + QA 录像；若接口或 RimWorld 版本变更需适时调整。

---

## 3. Gate 验收清单

| 阶段 | Gate 条件（全部满足才可合并主干） |
|------|-----------------------------------|
| **P5** | ① DebugPanel **Ask Colony Status** 按钮在一次调用内完成五步流程，返回自然语言总结 (`包含资源/心情/威胁关键词`) ② 内部日志显示 `ToolDecision → get_colony_status → ToolResult → FinalAnswer` 顺序 ③ 失败分支：若工具抛 `ToolExecutionException`，UI 显示友好错误文本，无崩溃 ④ 单元测试覆盖率 ≥ 85% (`OrchestrationServiceTests.cs`) |
| **P6** | ① 执行两轮对话后手动存档 → 退到主菜单 → 载入存档 → 调用 `IHistoryService.GetHistoryAsync([__PLAYER__, ColonyGovernor])` 返回 ≥2 条记录 ② 无 `IExposable` 泄漏在业务服务中 (`HistoryService` 不实现 `IExposable`) ③ `PersistenceManager` 在 `Log.msg` 输出 `Persist ok (entries: N)` 与 `Load ok (entries: N)` ④ 单元测试 `PersistenceRoundTripTests` 通过 |

---

## 4. 阶段交付一览

| 阶段 | 关键类 / 接口 | 新增/修改文件 | DebugPanel 变更 |
|------|---------------|--------------|----------------|
| P5 | `IOrchestrationService`, `OrchestrationService`, `OrchestrationExceptions.cs` | `Modules/Orchestration/*` | + Ask Colony Status |
| P5 | 扩充工具元数据解析 (`ToolCall`, `ToolCallArguments`) | `Modules/Tooling/Models/*` | — |
| P6 | `IPersistenceService`, `PersistenceService`, `PersistenceManager(GameComponent)` | `Infrastructure/Persistence/*`, `Lifecycle/PersistenceManager.cs` | + Record History |
| P6 | `HistoryService` - State DTO + Get/Load APIs | `Modules/World/HistoryService.cs` | — |

---

## 5. 详细任务拆解

### 5.1 P5 Orchestration (Minimal)

| # | 任务 | 预计时长 |
|---|------|----------|
| 5-1 | 定义接口 `IOrchestrationService`（Contracts 程序集） | 0.3d |
| 5-2 | 实现 `OrchestrationService`：五步编排；依赖注入 `ILLMService` `IPromptFactoryService` `IToolRegistryService` `IHistoryService` | 1.2d |
| 5-3 | 解析并验证 LLM `tool_calls`；封装到 `ToolCall` DTO | 0.5d |
| 5-4 | 错误处理：`try-catch` 把 `ToolExecutionException` 转化为二次提示词；包装最终错误 | 0.3d |
| 5-5 | 单元测试：
* `ReturnsSummary_WhenToolSucceeds`
* `ReturnsFriendlyMessage_WhenToolFails`
* `RecordsHistory_FinalOnly` | 0.4d |
| 5-6 | DebugPanel 按钮 **Ask Colony Status**：调用编排服务并流式显示回复 | 0.3d |

**完成判定**：按钮点击后 UI 打印五步日志；最终回复包含 `资源`、`心情`、`威胁` 字样；无未捕获异常。

### 5.2 P6 Persistence

| # | 任务 | 预计时长 |
|---|------|----------|
| 6-1 | 定义 `HistoryState` DTO（包含主存储 & 倒排索引） | 0.2d |
| 6-2 | `HistoryService` 提供 `GetStateForPersistence()` / `LoadStateFromPersistence()` | 0.4d |
| 6-3 | 实现 `PersistenceService`：封装 `Verse.Scribe.*` 调用；方法 `PersistHistoryState(...)` | 0.6d |
| 6-4 | 创建 `PersistenceManager` (`GameComponent`)：在 `ExposeData()` 调用服务方法 | 0.3d |
| 6-5 | ServiceContainer 注册单例 + 注入 | 0.1d |
| 6-6 | 单元测试 `PersistenceRoundTripTests`：序列化 → 反序列化后比较字典相等 | 0.3d |
| 6-7 | DebugPanel 按钮 **Record History**：写入两条示例历史并提示“请手动存档→读档验证” | 0.1d |

**完成判定**：读档后调用 `GetHistoryAsync` 返回原条目；无 `NullReference` 或版本不兼容字段。

---

## 6. Debug Panel 更新

1. **左侧按钮**：新增 `P5` & `P6` 分组；分别包含 **Ask Colony Status**、**Record History**。  
2. **日志区**：
   * P5 – 实时打印：`[Step1] LLM tool decision`, `[Step2] Execute tool ...`, `[Step3] Build prompt ...`, `[Step4] Stream delta ...`, `[Step5] History recorded`。
   * P6 – 输出 `Persist ok` / `Load ok` 消息以及历史条目数量。
3. **性能**：实时统计编排总耗时；目标 ≤ 3s（Mock 环境）。

---

## 7. 验收与交付

1. **CI**：`dotnet test` + `dotnet format`; 文档签名检查；Profiler 脚本验证编排平均耗时。  
2. **录像**：流程录像 `media/p5_p6_demo.mp4`：Ask Colony Status → Record History → 存档/读档 → 再查询。  
3. **Tag**：合并时打 `core/v4.0.0-beta`（完成 P5）或 `core/v4.0.0`（完成 P6 全阶段）。  
4. **文档**：同步更新 `IMPLEMENTATION_V4.md` 表格 Gate 状态与日期；CHANGELOG 追加 `v4.0.0-beta`/`v4.0.0`。

---

## 8. TODO Checklist

### P5 Orchestration  ⬜
- [ ] `IOrchestrationService` 接口 (Contracts)
- [ ] `OrchestrationService` 五步流程
- [ ] ToolCall 解析 & 验证
- [ ] Error Handling 分支
- [ ] 单元测试 3 项
- [ ] DebugPanel Ask Colony Status
- [ ] Profiler 脚本 ≤3s

### P6 Persistence  ⬜
- [ ] HistoryState DTO
- [ ] HistoryService 状态导入/导出 API
- [ ] PersistenceService + Scribe 调用
- [ ] PersistenceManager GameComponent
- [ ] UnitTest RoundTrip
- [ ] DebugPanel Record History
- [ ] CHANGELOG & 录像

> 完成本文件中所有 ✅ 项目后，即标记阶段 Gate 为 **通过**，并在此文件「TODO Checklist」处打勾保存版本。