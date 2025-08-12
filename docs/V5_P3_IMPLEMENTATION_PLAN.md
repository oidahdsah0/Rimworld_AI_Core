# RimAI V5 — P3 实施计划（Scheduler + WorldData）

> 目标：一次性交付“稳定、低抖动”的主线程调度器（ISchedulerService）与只读世界数据防腐层（IWorldDataService），确保任何 Verse 访问都在主线程安全执行，并向上游提供去 Verse 化 DTO 的只读数据。本文档为唯一入口文档，无需翻阅旧文即可落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 新增 `ISchedulerService` + `SchedulerService` + `SchedulerGameComponent`（主线程泵）
  - 新增 `IWorldDataService` + `WorldDataService`（只读、线程安全、内部唯一允许使用 `Verse` 的读取实现）
  - Debug 面板：Scheduler 面板与 WorldData 最小验证按钮
  - 配置：内部 `CoreConfig.Scheduler` 与 `CoreConfig.WorldData` 默认值（对外 Snapshot 不新增字段）
  - 可观测：队列长度/耗时/每帧预算与报警日志

- 非目标（后续阶段处理）
  - 写世界/指令执行（交由 `ICommandService` 与工具系统）
  - 舞台扫描/邻近度/选题（P11+）
  - 历史/个性化/编排策略接入（P10+/P12）

---

## 1. 架构总览（全局视角）

- 边界
  - `ISchedulerService` 是 Core 内部唯一主线程调度入口；任何需要接触 Verse/Unity 的代码必须经由它执行
  - `IWorldDataService` 是只读世界数据的唯一入口；输出为去 Verse 化的安全 DTO/基础类型

- 设计原则
  - 所有 Verse 访问统一主线程化；后台线程禁止直接访问 Verse
  - 队列化 + 帧预算（最大任务数/时间预算），确保零感知卡顿
  - 失败快速可观测：长任务/超时/队列饱和/未加载世界 → 清晰错误与日志

---

## 2. 接口契约（内部 API）

> 下述接口位于 Core 内部，不进入 Contracts 稳定层。

```csharp
// RimAI.Core/Source/Infrastructure/Scheduler/ISchedulerService.cs
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Infrastructure.Scheduler {
  internal interface ISchedulerService {
    bool IsMainThread { get; }

    // fire-and-forget（主线程执行）
    void ScheduleOnMainThread(Action action);

    // 带返回值的主线程执行
    Task<T> ScheduleOnMainThreadAsync<T>(Func<T> func, CancellationToken ct = default);

    // 可选：异步工作包装到主线程
    Task ScheduleOnMainThreadAsync(Func<Task> func, CancellationToken ct = default);

    // 主线程延迟（按 Tick 或 ms 实现其一即可；建议按 Tick）
    Task DelayOnMainThreadAsync(int ticks, CancellationToken ct = default);

    // 周期任务（按游戏 Tick 周期），返回 ID 或 IDisposable 便于停止
    IDisposable SchedulePeriodic(string name, int everyTicks, Func<CancellationToken, Task> work, CancellationToken ct = default);
  }
}

// RimAI.Core/Source/Modules/World/IWorldDataService.cs
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.World {
  internal interface IWorldDataService {
    // P3 最小验收能力
    Task<string> GetPlayerNameAsync(CancellationToken ct = default);

    // 预留扩展（后续阶段渐进开启）
    Task<string> GetPawnDisplayNameAsync(string pawnId, CancellationToken ct = default);
    // Task<ColonySummary> GetColonySummaryAsync(CancellationToken ct = default);
  }
}
```

接口约束：
- 所有对 Verse 的调用只能出现在 `WorldDataService` 内部，且必须通过 `ISchedulerService` 主线程化
- `IWorldDataService` 对外只返回 C# 基础类型或内部 DTO；严禁返回 Verse 类型

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Infrastructure/
      Scheduler/
        ISchedulerService.cs
        SchedulerService.cs
        SchedulerGameComponent.cs     // 在 Update/Tick 中泵队列
        SchedulerMetrics.cs           // 可选：指标聚合
    Modules/
      World/
        IWorldDataService.cs
        WorldDataService.cs           // 唯一允许 using Verse 的读取实现（只读）
        WorldDtos.cs                  // 内部 DTO（去 Verse 化）
    UI/DebugPanel/Parts/
      P3_SchedulerPanel.cs
      P3_WorldDataPanel.cs
```

---

## 4. 配置（内部 CoreConfig）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

建议默认值：

```json
{
  "Scheduler": {
    "MaxTasksPerUpdate": 64,
    "MaxBudgetMsPerUpdate": 0.5,
    "MaxQueueLength": 2000,
    "LongTaskWarnMs": 5,
    "EnablePriorityQueue": false
  },
  "WorldData": {
    "DefaultTimeoutMs": 2000,
    "NameFallbackLocale": "zh-Hans"
  }
}
```

说明：
- `MaxBudgetMsPerUpdate` 是软预算（超出打印 Warn）；硬限制为 `MaxTasksPerUpdate`
- `EnablePriorityQueue=false` 下仅使用单一 FIFO 队列

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S10，期间可通过 Debug 面板与日志进行自检。无需查阅其他文档。

### S1：定义接口与骨架

1) 新建 `ISchedulerService` 与 `IWorldDataService` 接口文件（见 §2）
2) 新建实现骨架 `SchedulerService`、`WorldDataService`
3) `SchedulerService` 构造中捕获 `MainThreadId`（在 GameComponent 初始化时传入或在首帧记录）

### S2：工作项模型与队列

1) 定义 `WorkItem`：包含 `Action/Func`、`TaskCompletionSource`、`CancellationToken`、`Name`、`EnqueueTick`
2) 采用 `ConcurrentQueue<WorkItem>`（或 3 路队列：High/Normal/Low 当启用优先级）
3) 入队时检查 `MaxQueueLength`，超过记录 Warn（开发期默认不丢弃）

### S3：主线程泵（SchedulerGameComponent）

1) 新建 `SchedulerGameComponent` 并在 Mod 启动注册
2) 在 `Update()`（或 `GameComponentTick`）中：
   - 记录帧开始时间；计数 `processed=0`
   - while 队列非空 且 `processed < MaxTasksPerUpdate`
     - TryDequeue → 执行（捕获异常，写入 TCS）
     - 记录单任务耗时，大于 `LongTaskWarnMs` 打印 Warn（含 Name/入队时距今 Tick）
     - `processed++`；如耗时超过 `MaxBudgetMsPerUpdate` 打印 Warn 并 `break`
3) 将 `IsMainThread=true` 的判断绑定当前线程 ID

伪代码：
```csharp
var swFrame = Stopwatch.StartNew();
int processed = 0;
while (processed < cfg.Scheduler.MaxTasksPerUpdate && _queue.TryDequeue(out var item)) {
  try {
    var swTask = Stopwatch.StartNew();
    item.ExecuteOnMainThread(); // action/func with TCS
    swTask.Stop();
    if (swTask.ElapsedMilliseconds > cfg.Scheduler.LongTaskWarnMs) {
      Log.Warn($"[RimAI.Core][Scheduler] Long task {item.Name} took {swTask.ElapsedMilliseconds}ms");
    }
  } catch (Exception ex) {
    item.TrySetException(ex);
    Log.Error($"[RimAI.Core][Scheduler] Task {item.Name} failed: {ex}");
  }
  processed++;
  if (swFrame.Elapsed.TotalMilliseconds > cfg.Scheduler.MaxBudgetMsPerUpdate) {
    Log.Warn($"[RimAI.Core][Scheduler] Frame budget exceeded: {swFrame.Elapsed.TotalMilliseconds}ms");
    break;
  }
}
```

### S4：API 实现（ScheduleOnMainThread*）

1) `ScheduleOnMainThread(Action)`：封装为 `WorkItem` 入队；无返回值
2) `ScheduleOnMainThreadAsync<T>(Func<T>)`：创建 `TaskCompletionSource<T>`，入队；在泵中设置结果/异常/取消
3) `ScheduleOnMainThreadAsync(Func<Task>)`：在泵执行时 `await` 该任务（注意：避免阻塞帧，建议短任务；长任务拆分）
4) `DelayOnMainThreadAsync(int ticks)`：记录目标 Tick，到达时完成 TCS（可使用周期检查或专属延迟队列）

### S5：周期任务（SchedulePeriodic）

1) 维护一个 `List<PeriodicTask>`：`{ name, everyTicks, lastRunTick, work, cts }`
2) 在每帧/每 Tick 检查是否到达 `lastRunTick + everyTicks`，若是则入队执行
3) 返回 `IDisposable`：`Dispose()` 时取消并从列表移除

### S6：IWorldDataService — 最小能力

1) `GetPlayerNameAsync`：
   - 在 `WorldDataService` 内部使用 `ISchedulerService.ScheduleOnMainThreadAsync<string>(() => {... Verse ...})`
   - 检查世界是否加载/玩家派系是否存在
   - 读取玩家显示名（保持与 RimWorld 当前版本兼容的 API）
   - 为空/异常 → 抛出 `WorldDataException`（含上下文：mapLoaded=false 等）
2) 预留方法暂不实现或返回 `NotImplementedException`（后续阶段开启）

示例（简化伪代码）：
```csharp
public Task<string> GetPlayerNameAsync(CancellationToken ct = default) =>
  _scheduler.ScheduleOnMainThreadAsync(() => {
    if (Current.Game == null) throw new WorldDataException("World not loaded");
    var name = Faction.OfPlayer?.Name ?? "Player";
    return name;
  }, ct);
```

### S7：异常与取消/超时

1) `WorldDataService` 统一包装 Verse 侧异常为 `WorldDataException`
2) 调用 `ScheduleOnMainThreadAsync` 时传入 `ct`；未提供时内部套用 `WorldData.DefaultTimeoutMs`
3) `SchedulerService` 入队时若检测到 `ct.IsCancellationRequested`，直接拒绝并抛 `OperationCanceledException`

### S8：指标与日志

1) 记录：队列长度、每帧处理数、平均/95P 任务耗时、长任务日志、预算超标日志
2) Debug 面板读数：以文本/表格形式展示（可按需延后为图表）

### S9：Debug 面板（P3 专用）

- Scheduler 面板
  - `PingOnMainThread`：入队一个动作，打印当前线程与 `IsMainThread=true`，显示耗时
  - `SpikeTest`：批量入队 N=1000 个短任务，观测峰值队列长度与预算日志
  - 显示实时统计：队列长度/每帧处理数/平均耗时/长任务计数
- WorldData 面板
  - `GetPlayerName` 按钮：调用 `GetPlayerNameAsync`，显示名称与耗时；错误则展示异常摘要

### S10：DI 注册与启动检查

1) `ServiceContainer.Init()` 注册：`ISchedulerService -> SchedulerService`、`IWorldDataService -> WorldDataService`
2) 启动时 `Resolve<ISchedulerService>()` 并打印启动横幅：`[P3] Scheduler ready`
3) 确保 `SchedulerGameComponent` 已挂载并在 `Update/Tick` 中运行（日志一次性打印）

---

## 6. 验收 Gate（必须全绿）

- Scheduler
  - 运行 10 分钟：无明显帧抖动；预算日志无持续超标（偶发 Warn 可接受）
  - `PingOnMainThread` 显示 `IsMainThread=true` 且耗时 < 2 ms
  - `SpikeTest`：入队 1000 个短任务后，队列可在 ≤ 5 秒内清空；峰值队列长度 < `MaxQueueLength`
- WorldData
  - `GetPlayerNameAsync` 返回正确玩家显示名；冷/热两次均 < 100 ms
  - 世界未加载/地图为空 → `WorldDataException`，Debug 面板可读提示
- 配置热生效
  - 修改 `Scheduler.MaxTasksPerUpdate`/`MaxBudgetMsPerUpdate` 保存后，无需重启即从面板读到新值并生效

---

## 7. 回归脚本（人工/录屏）

1) 启动新存档 → 打开 Debug 面板 → `PingOnMainThread`（截图日志）
2) 执行 `GetPlayerName`（两次）→ 记录名称与耗时
3) 执行 `SpikeTest` → 观察峰值队列长度与预算日志；等待队列回落至 0
4) 在设置页调小 `MaxTasksPerUpdate`，保存 → 面板确认生效 → 重复 `SpikeTest` 对比处理速率

---

## 8. CI/Gate（使用 Cursor 内置工具，必须通过）

- Verse 访问最小面
  - 全仓检查：`using\s+Verse` 仅允许出现在：`WorldDataService.cs` 与 `SchedulerGameComponent.cs`
- 主线程化纪律
  - 全仓检查：Verse API 名称出现处的同文件必须存在 `ISchedulerService` 调用（人工抽样/工具辅助）
- 注入纪律
  - 禁止属性注入；仅构造函数注入
- 组件单例性
  - 仅存在一个 `SchedulerGameComponent`

---

## 9. 风险与缓解

- 长任务阻塞帧 → 拆分为多个短任务或改为后台执行并只在主线程做最小段
- 队列风暴 → 启用优先级队列与速率限制；对低优先级请求做降频或拒绝策略
- 世界未加载空引用 → 统一前置校验并抛出 `WorldDataException`，避免 NRE 污染日志
- 预算误设过小 → 面板观测后适当上调；保持 ≤ 1ms 帧预算总目标

---

## 10. FAQ（常见问题）

- Q：为什么必须通过 Scheduler 才能访问 Verse？
  - A：RimWorld/Verse 非线程安全；跨线程访问会导致隐性崩溃或未定义行为。
- Q：长任务如何处理？
  - A：拆分/分帧/后台预处理；主线程仅处理必要的 Verse 读取与 UI 同步。
- Q：是否需要优先级队列？
  - A：默认关闭；当存在 UI 交互与后台扫描竞争时建议开启（High 给 UI）。

---

## 11. 变更记录（提交要求）

- 初版（v5-P3）：交付 Scheduler + WorldData + Debug 面板 + CI/Gate（Cursor 内置工具）；不改对外 Contracts
- 后续修改：新增内部配置字段需向后兼容，并在本文“配置”与 Gate 中同步更新

---

本文件为 V5 P3 唯一权威实施说明。实现与验收以本文为准。
