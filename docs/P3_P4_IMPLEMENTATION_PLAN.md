# RimAI.Core V4 – P3~P4 详细实施方案 (DRAFT)

> 版本：v4.0.0-beta 预备  
> 更新日期：2025-08-05  
> 范围：完成 Scheduler+WorldData (P3) 与 Tool System (P4)，打通安全读写链路，为后续 Orchestration 做铺垫。

---

## 1. 背景与目标
在 v4.0.0-alpha 中，我们已完成 **P0~P2**，实现 Mod 加载、依赖注入、配置系统与最小 LLM 网关。下一步将实现：

* **P3 Scheduler & WorldData**：解决主线程访问 RimWorld API 的安全问题，并暴露首个只读方法 `GetPlayerName()`。
* **P4 Tool System**：引入可扩展工具框架 (`IToolRegistryService`) 及首个示例工具 `GetColonyStatusTool`，实现从 LLM 决策到游戏数据读取的完整链路。

---

## 2. 总里程碑与时间预估
| 阶段 | 目标 MVP | DebugPanel 按钮 | 预计工时 |
|------|---------|----------------|----------|
| P3 | `ISchedulerService` + `IWorldDataService.GetPlayerName` | Get Player Name | 2 天 |
| P4 | 工具注册 + `get_colony_status` 工具 | Run Tool | 2 天 |
| **合计** | — | — | **4 天** |

> *并行性提示*：P3 的 Scheduler 与 WorldData 开发可并行；P4 的 ToolRegistry 可与工具实现并行。

---

## 3. 阶段交付一览
| 阶段 | 关键类 / 接口 | 新增/修改文件 | DebugPanel 变更 |
|------|---------------|--------------|----------------|
| P3 | `ISchedulerService`, `IWorldDataService` | `Infrastructure/SchedulerService.cs`, `Modules/World/IWorldDataService.cs` 等 | + Get Player Name |
| P4 | `IToolRegistryService`, `IRimAITool`, `GetColonyStatusTool` | `Modules/Tooling/*` | + Run Tool |

---

## 4. 详细任务拆解

### 4.1 P3 Scheduler & WorldData
| # | 任务 | 预计时长 |
|---|------|----------|
| 3-1 | 创建 `ISchedulerService` 接口 + `SchedulerService` 实现（线程安全队列 + `GameComponent` 驱动） | 0.5d |
| 3-2 | 在 `ServiceContainer.Init()` 注册单例 | 0.1d |
| 3-3 | 创建 `IWorldDataService` 接口；实现最小方法 `Task<string> GetPlayerNameAsync()` | 0.4d |
| 3-4 | `WorldDataService` 内部使用 `ISchedulerService` 调度到主线程 | 0.3d |
| 3-5 | DebugPanel 按钮 **Get Player Name**：调用服务并在窗口右侧输出 | 0.2d |
| **完成判定** | 按钮点击后能显示当前玩家派系名称，线程安全无红字 | — |

### 4.2 P4 Tool System
| # | 任务 | 预计时长 |
|---|------|----------|
| 4-1 | 定义 `IRimAITool`、`ToolFunction` DTO（若合同层尚未存在） | 0.3d |
| 4-2 | 实现 `IToolRegistryService`：启动时反射扫描并注册工具；`ExecuteToolAsync` | 0.7d |
| 4-3 | 创建示例工具 `GetColonyStatusTool`（依赖 `IWorldDataService`） | 0.4d |
| 4-4 | 在 `ServiceContainer.Init()` 注册 `IToolRegistryService` 单例 | 0.1d |
| 4-5 | DebugPanel 按钮 **Run Tool**：执行 `get_colony_status` 并输出 JSON 结果 | 0.3d |
| **完成判定** | 按钮可返回殖民地资源/心情等汇总 JSON；无红字 | — |

---

## 5. Debug Panel 更新规范
* 左侧按钮追加：`P3` Get Player Name、`P4` Run Tool。
* 按钮执行日志打印至右侧滚动框，保留历史 100 行。

---

## 6. 验收标准
1. `core/v4.0.0-beta` tag 代码编译通过 (`msbuild` / `dotnet build`)。
2. RimWorld 启动无红字；DebugPanel 新按钮全部可正常执行。
3. 录屏演示：
   * Ping → Reload Config → Chat Echo → Get Player Name → Run Tool 全流程。
4. 更新 `CHANGELOG.md`：新增 v4.0.0-beta 条目。

---

> **后续**：P4 完成后，进入 P5 Orchestration & Tool-Assisted Query，届时将撰写 `P5_IMPLEMENTATION_PLAN.md`。