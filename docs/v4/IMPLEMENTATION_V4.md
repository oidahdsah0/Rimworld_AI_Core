# RimAI.Core V4 逐步实施计划（IMPLEMENTATION_V4.md）

> 最新更新：2025-08-05  
> 本文件与 `ARCHITECTURE_V4.md` 保持一一对应，任何阶段状态变化都必须同时修改两处文档。

---

## 1. 阶段总览

| 阶段 | 目标 MVP（可运行最小闭环） | Gate 验收清单（全部通过方可合并） | Debug 面板/脚本 | 预计工期 |
|------|----------------------------|------------------------------------|----------------|----------|
| **P0 Skeleton** | Mod 成功加载；DI 初始化 | ① RimAIMod 在日志中输出 "RimAI v4 Skeleton Loaded" ② `CoreServices` 可获取 `ServiceContainer` 实例 ③ 无红色错误 | Ping | 0.5 天 |
| **P1 DI & Config** | 配置服务可热重载 | ① ServiceContainer 反射注册全部通过 ② 调用 `IConfigurationService.Reload()` 后事件触发 ③ Debug 面板按钮显示最新配置 | Reload Config | 1 天 |
| **P2 LLM Gateway** | ILLMService Echo 测试 | ① `GetResponseAsync("hello")` 返回 "hello" ② 缓存命中率可在面板实时刷新 ③ 重试 3 次退避策略日志可见 ④ DebugPanel 覆盖 流式 / 非流式 / JSON / Tools / 批量 请求测试均通过 | Chat Echo + LLM Tests | 2 天 |
| **P3 Scheduler + WorldData** | 主线程安全数据读取 | ① `ISchedulerService.ScheduleOnMainThread` 不阻塞 UI ② `IWorldDataService.GetPlayerNameAsync()` 返回玩家派系名称 ③ Profiler 每帧耗时 ≤ 1 ms | Get Player Name | 2 天 |
| **P4 Tool System** | 工具注册与执行 | ① 启动时自动扫描并注册 `GetColonyStatusTool` ② `ToolRegistryService.ExecuteToolAsync` 返回正确 JSON ③ 工具执行期间无跨线程异常 | Run Tool | 2 天 |
| **P5 Orchestration (Min)** | 五步工作流闭环 | ① `ExecuteToolAssistedQueryAsync` 在 1 调用内完成工具决策 → 执行 → LLM 回复 ② 用户可通过面板提问“殖民地概况”并得到自然语言总结 ③ 熔断器 TODO 标记通过单元测试 | Ask Colony Status | 3 天 |
| **P6 Persistence** | History 持久化 | ① 退出游戏后重新加载，上一局对话历史完整恢复 ② `IPersistenceService` 仅在 `Source/Infrastructure/Persistence` 目录调用 `Verse.Scribe` API | Record History | 2 天 |
| **P7 Event Aggregator** | 高频事件节流 | ① 连续 5 次伤病事件仅触发 1 次 LLM 调用 ② 冷却窗口可在 Debug 面板重置 ③ AggregatedEvents 列表顺序按优先级降序 | List Aggregated Events | 2 天 |
| **P8 Persona & Stream UI** | 人格生效 + 流式渲染 | ① `IPersonaService` 加载模板并在对话中插入 `SystemPrompt` ② UI 逐块渲染 `StreamResponseAsync` ③ Assistant persona 与 Pawn persona 可切换 | Chat with Assistant | 3 天 |

> 说明：每阶段提交 PR 时必须附带 **Gate 录像**，演示 Debug 面板按钮/脚本全绿。  
> ⚠️ **兼容性要求**：所有提交必须在 .NET Framework 4.7.2 环境编译通过；严禁引入 4.7.2 以上版本专属 API/语法。

---

## 2. 版本里程碑

| 版本 | 覆盖阶段 | Tag 命名 | 发布渠道 |
|------|----------|----------|----------|
| v4.0.0-alpha | P0 ~ P2 | `core/v4.0.0-alpha` | 内部测试分支 |
| v4.0.0-beta  | P0 ~ P5 | `core/v4.0.0-beta` | Steam 非公开 |
| v4.0.0 | P0 ~ P8 | `core/v4.0.0` | Steam 公开预览 |

Tag 策略与 CI 流程详见 `ARCHITECTURE_V4.md` 第 8 节。

---

## 3. 提交流程

1. **分支命名**：`feature/P{阶段序号}_{简述}`，例如 `feature/P3_Scheduler`。
2. **单元测试**：新代码必须带覆盖率 ≥ 80% 的测试；阶段 Gate 对应的脚本测试存放于 `Tests/Stages`。
3. **CI**：GitHub Actions 执行 `dotnet test`、`dotnet format --no-restore`，并校验文档签名。
4. **代码评审**：至少 1 名核心维护者批准后方可合并。
5. **文档同步**：如修改接口或 Gate，务必同时更新本文件与 `ARCHITECTURE_V4.md`，否则 CI 失败。

---

## 4. 进度日志

| 日期 | 状态 |
|------|------|
| 2025-08-05 | P0~P2 已完成并合并，生成 `core/v4.0.0-alpha`，Ping / Reload Config / Chat Echo 按钮全部通过。 |

> 后续新增阶段或修订，请在日志尾部追加行，不得覆盖历史。
