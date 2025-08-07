# RimAI.Core V4 – P3~P4 详细实施方案

> 版本：v4.0.0-beta 预备  
> 更新日期：2025-08-06  
> 本文与 `ARCHITECTURE_V4.md` / `IMPLEMENTATION_V4.md` 同步，任何接口或 Gate 变更需同时修订。

---

## 1. 背景与目标

在 v4.0.0-alpha 中，P0~P2 已实现 LLM Echo 闭环。P3~P4 专注于**安全访问游戏数据**与**工具框架**，为 Orchestration 奠定“读/写”基础。

目标：
* **P3 Scheduler & WorldData** – 主线程安全读 API；首个方法 `GetPlayerNameAsync()`。
* **P4 Tool System** – 自动发现 + 注册工具；示例 `get_colony_status` 工具完整运行。

---

## 2. 里程碑与时间预估

| 阶段 | 目标 MVP | Debug 面板按钮 | 预计工时 |
|------|----------|----------------|----------|
| **P3** | Scheduler + `GetPlayerName` | Get Player Name | 2 天 |
| **P4** | ToolRegistry + `get_colony_status` | Run Tool | 2 天 |
| **合计** | — | — | **4 天** |

---

## 3. Gate 验收清单

| 阶段 | Gate 条件 |
|------|-----------|
| **P3** | ① `ISchedulerService.ScheduleOnMainThread` 不阻塞 UI；Profiler 每帧新增耗时 ≤ **1 ms** ② `IWorldDataService.GetPlayerNameAsync()` 返回正确派系名 ③ DebugPanel 按钮执行无红字 |
| **P4** | ① 启动时 `ToolRegistryService` 自动扫描并注册 `GetColonyStatusTool` ② `ExecuteToolAsync` 运行时 **无跨线程异常** ③ 返回 JSON 包含 `resources`, `mood`, `threats` 三字段 ④ DebugPanel 显示工具执行耗时 < 200ms (Mock) |

---

## 4. 阶段交付一览

| 阶段 | 关键类 / 接口 | 新增/修改文件 | DebugPanel 变更 |
|------|---------------|--------------|----------------|
| P3 | `ISchedulerService`, `SchedulerService`, `IWorldDataService`, `WorldDataService` | `Infrastructure/SchedulerService.cs`, `Modules/World/WorldDataService.cs`, `Lifecycle/SchedulerComponent.cs` | + Get Player Name |
| P4 | `IRimAITool`, `ToolFunction`, `IToolRegistryService`, `ToolRegistryService`, `GetColonyStatusTool` | `Modules/Tooling/*` | + Run Tool |

---

## 5. 详细任务拆解

### 5.1 P3 Scheduler & WorldData
| # | 任务 | 预计时长 |
|---|------|----------|
| 3-1 | 实现 `SchedulerService`：`ConcurrentQueue<Action>` + `GameComponent.Update` | 0.5d |
| 3-2 | 注册单例 (`ServiceContainer.Register<ISchedulerService, SchedulerService>()`) | 0.1d |
| 3-3 | `WorldDataService`：内部调度到主线程，返回玩家派系名 | 0.4d |
| 3-4 | 单元测试：后台线程调用 `GetPlayerNameAsync()` 返回非空 | 0.2d |
| 3-5 | DebugPanel 按钮 **Get Player Name**：调用并打印结果 | 0.2d |

**完成判定**：按钮点击后显示正确派系名; Unity Profiler 采样帧耗时增量 ≤1ms。

### 5.2 P4 Tool System
| # | 任务 | 预计时长 |
|---|------|----------|
| 4-1 | 定义 `IRimAITool`（接口）、`ToolFunction`（参数 Schema） | 0.3d |
| 4-2 | `ToolRegistryService`：启动时反射扫描、构造注入工具实例 | 0.7d |
| 4-3 | 实现 `GetColonyStatusTool`（依赖 `IWorldDataService`） | 0.4d |
| 4-4 | DebugPanel 按钮 **Run Tool**：执行工具并格式化输出 JSON | 0.3d |

**完成判定**：返回 JSON 样例：
```json
{
  "resources": {"steel": 1200, "food": 350},
  "mood": 0.82,
  "threats": "Low"
}
```
执行过程无主线程冲突。

---

## 6. Debug Panel 更新
* **左侧按钮**：新增 `P3` & `P4` 分组。  
* **右侧日志**：记录耗时、线程 ID 与结果 JSON。  
* 保留历史 100 行，超出滚动删除。

---

## 7. 验收与交付

1. **CI**：`dotnet test`; Profiler 自动脚本验证帧耗时。  
2. **录像**：Ping → Reload Config → Chat Echo → Get Player Name → Run Tool 全流程。  
3. **Tag**：合并时打 `core/v4.0.0-beta`。  
4. **文档**：更新 CHANGELOG 与本文件 Gate 完成（✅）。

---

## 8. TODO Checklist

### P3 Scheduler & WorldData  ✅
- [ ] SchedulerService 实现 & 注册
- [ ] WorldDataService 实现首个 API
- [ ] UnitTest: bg-thread safe call
- [ ] DebugPanel Get Player Name
- [ ] Profiler 1ms 验证脚本

### P4 Tool System  ✅ （2025-08-06 完成）
- [x] Interfaces + DTOs 定义（IRimAITool / IToolRegistryService / ToolFunction）
- [x] ToolRegistryService 自动扫描并注册工具
- [x] GetColonyStatusTool 实现
- [x] DebugPanel Run Tool 按钮
- [x] UnitTest: discover & execute tool
- [x] **Core.Contracts v0.1 释出**（仅 IRimAITool + IToolRegistryService + ToolFunction）
