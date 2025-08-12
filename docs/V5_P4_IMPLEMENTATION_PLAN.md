# RimAI V5 — P4 实施计划（Tool System）

> 目标：一次性交付“稳定、可扩展、可观测”的工具系统骨架：自动发现与注册、统一 Schema 暴露、参数校验、受控执行（并发/主线程/副作用/超时/速率）与 Debug 面板自检。本文档为唯一入口文档，无需翻阅旧文即可落地与验收。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 工具契约与元数据：`IRimAITool`、`ToolMeta`、`ToolContext`、并发/副作用/来源声明
  - 工具注册与发现：`IToolRegistryService` + 反射扫描 + DI 构造 + 黑白名单
  - Schema 暴露与参数校验：沿用 `RimAI.Framework.Contracts.ToolFunction`；JSON Schema 校验与绑定
  - 执行沙箱：主线程调度、并发互斥、资源锁、速率限制、超时、错误映射
  - 可观测：统一日志与 Debug 面板（列表/筛选/测试执行）

- 非目标（后续阶段处理）
  - 工具向量索引/匹配模式（P9）
  - 最终自然语言总结与编排（P12 工具仅编排；本阶段仅返回结构化结果）
  - 大规模“写世界”示例与权限模板（后续逐步上线）

---

## 1. 架构总览（全局视角）

- 边界与依赖
  - 工具系统对上游暴露统一清单与执行入口；上游（UI/Stage/Orchestration）只与 `IToolRegistryService` 交互
  - 工具内严禁直接访问 Verse/Unity：
    - 只读数据 → 通过 `IWorldDataService`（P3）
    - 写世界/副作用 → 通过 `ICommandService`（后续阶段接入）；本阶段若工具声明副作用，仅记录/拒绝或走沙箱占位
  - 主线程访问 → 统一经 `ISchedulerService`（P3）

- 设计原则
  - 可发现、可禁用、可观测：开箱即用，Debug 面板可一键验证
  - 安全沙箱：并发/互斥/超时/速率统一在注册表层面治理，工具实现保持纯净
  - 契约稳定：为 P9/P12 预留扩展位（无需破坏本阶段契约）

---

## 2. 接口契约（内部 API）

> 下述接口位于 Core 内部，不进入 Contracts 稳定层；Schema 类型沿用 `RimAI.Framework.Contracts`。

```csharp
// RimAI.Core/Source/Modules/Tooling/IRimAITool.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts; // ToolFunction, ToolCall

namespace RimAI.Core.Modules.Tooling {
  internal enum ToolConcurrency { ReadOnly, RequiresMainThread, Exclusive }
  internal enum ToolOrigin { PlayerUI, Stage, AIServer, EventAggregator, Other }

  internal sealed class ToolMeta {
    public ToolConcurrency Concurrency { get; init; } = ToolConcurrency.ReadOnly;
    public bool HasSideEffects { get; init; } = false;     // 是否可能改写世界/产生外部影响
    public string ResourceKey { get; init; }               // 可选：互斥粒度（同 key 互斥）
    public int DefaultTimeoutMs { get; init; } = 3000;     // 缺省超时
    public int RateLimitPerMinute { get; init; } = 60;     // 简单速率限制（按工具名/资源键）
    public IReadOnlyList<ToolOrigin> AllowedOrigins { get; init; } = new[] { ToolOrigin.PlayerUI, ToolOrigin.Stage, ToolOrigin.AIServer, ToolOrigin.EventAggregator, ToolOrigin.Other };
  }

  internal sealed class ToolContext {
    public string ConversationId { get; init; }
    public IReadOnlyList<string> ParticipantIds { get; init; }
    public string Locale { get; init; }
    public ToolOrigin Origin { get; init; }

    // 服务入口（只读/主线程/命令）
    public Infrastructure.Scheduler.ISchedulerService Scheduler { get; init; }
    public Modules.World.IWorldDataService WorldData { get; init; }
    public object CommandService { get; init; } // 占位：后续阶段替换为 ICommandService

    // 预算与控制
    public int TimeoutMs { get; init; }
    public CancellationToken CancellationToken { get; init; }
  }

  internal interface IRimAITool {
    string Name { get; }
    string Description { get; }

    ToolFunction GetSchema();                 // JSON Schema（Framework Contracts）
    ToolMeta GetMeta();                       // 并发/副作用/超时/资源/来源
    bool IsAvailable(ToolContext ctx);        // 可选：硬件/电力/场景限制

    // 返回序列化友好的对象（字符串/POCO/匿名对象），供上游总结或直出
    Task<object> ExecuteAsync(Dictionary<string, object> args, ToolContext ctx, CancellationToken ct);
  }
}

// RimAI.Core/Source/Modules/Tooling/IToolRegistryService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts; // ToolFunction

namespace RimAI.Core.Modules.Tooling {
  internal sealed class ToolQueryOptions {
    public ToolOrigin Origin { get; init; } = ToolOrigin.PlayerUI;
    public IReadOnlyList<string> IncludeWhitelist { get; init; } // 可空
    public IReadOnlyList<string> ExcludeBlacklist { get; init; } // 可空
  }

  internal sealed class ToolCallOptions {
    public ToolContext Context { get; init; }
    public int? TimeoutMsOverride { get; init; }
  }

  internal sealed class ToolInfo {
    public string Name { get; init; }
    public ToolFunction Schema { get; init; }
    public ToolMeta Meta { get; init; }
    public bool Available { get; init; }
  }

  internal interface IToolRegistryService {
    IReadOnlyList<ToolFunction> GetAllToolSchemas(ToolQueryOptions options = null);
    ToolInfo GetToolInfo(string toolName);

    Task<object> ExecuteToolAsync(
      string toolName,
      Dictionary<string, object> args,
      ToolCallOptions options,
      CancellationToken ct = default);
  }
}
```

说明：
- 参数校验基于 `ToolFunction` 的 JSON Schema；校验失败必须短路返回结构化错误（不执行工具）
- 工具返回对象不强制类型；上游（P12）负责将结果纳入提示词或直出

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Tooling/
        IRimAITool.cs
        IToolRegistryService.cs
        ToolRegistryService.cs
        ToolDiscovery.cs            // 反射扫描 + DI 构造
        ToolSandbox.cs              // 并发/主线程/互斥/速率/超时治理
        ToolBinding.cs              // 参数绑定/JSON Schema 校验
        ToolLogging.cs              // 统一日志/事件
        DemoTools/
          GetColonyStatusTool.cs    // 只读示例
    UI/DebugPanel/Parts/
      P4_ToolExplorer.cs           // 列表/筛选/查看 Schema/Meta/可用性
      P4_ToolRunner.cs             // 参数编辑与执行按钮
```

---

## 4. 配置（内部 CoreConfig.Tooling）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

建议默认值：

```json
{
  "Tooling": {
    "Enabled": true,
    "DefaultTimeoutMs": 3000,
    "MaxConcurrent": 8,
    "Whitelist": [],
    "Blacklist": [],
    "DangerousToolConfirmation": false,
    "PerToolOverrides": {
      // "GetColonyStatus": { "timeoutMs": 2000, "rateLimitPerMinute": 120 }
    }
  }
}
```

说明：
- `Whitelist/Blacklist` 按工具名匹配；白名单优先
- `PerToolOverrides` 仅覆盖超时/速率等运行时配置，不改变工具声明的 Meta

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S12；期间可通过 Debug 面板与日志进行自检。

### S1：定义契约与元数据

1) 新建 `IRimAITool`/`IToolRegistryService`/`ToolMeta`/`ToolContext`/`ToolConcurrency`/`ToolOrigin`
2) 契约遵循：Schema = `ToolFunction`；返回 `object`；声明副作用/并发/资源键

### S2：实现工具发现（ToolDiscovery）

1) 反射扫描已加载程序集，寻找 `IRimAITool` 非抽象公开类
2) 通过 `ServiceContainer` 构造实例（支持依赖注入）
3) 记录发现日志；遇到构造失败打印错误并跳过

### S3：注册表与清单（ToolRegistryService）

1) 内部字典：`name → tool instance`、`name → ToolInfo`（含 Schema/Meta/可用性）
2) 应用黑白名单过滤；记录禁用原因
3) `GetAllToolSchemas` 根据 `ToolQueryOptions` 过滤可见集（含 Origin）

### S4：参数校验与绑定（ToolBinding）

1) 基于 `ToolFunction` 的参数定义对 `Dictionary<string,object>` 做校验：
   - 必填/可选、类型/枚举、范围/格式
   - 校验失败返回结构化错误 `{ code:"validation_error", field:"...", message:"..." }`
2) 将校验通过的参数以原始字典传给工具；如需 POCO 由工具自身解析

> 实现提示：可用 System.Text.Json + 轻量校验逻辑；或接入小型 JSON Schema 校验库（保持 4.7.2 兼容）

### S5：执行沙箱（ToolSandbox）

1) 并发与资源锁：
   - `Exclusive` 或存在 `ResourceKey` → 使用 `ConcurrentDictionary<string,SemaphoreSlim>` 做互斥
   - `ReadOnly` 默认无互斥；`RequiresMainThread` 通过 Scheduler 串行化在主线程
2) 主线程调度：
   - `RequiresMainThread` → `ISchedulerService.ScheduleOnMainThreadAsync`
   - 其它情况可在线程池执行；禁止工具内部触达 Verse
3) 速率限制：
   - 按 `name` 或 `resourceKey` 维度的滑动窗口计数；超过阈值返回 `{ code:"rate_limited" }`
4) 超时：
   - 结合 `ToolMeta.DefaultTimeoutMs` 与 `PerToolOverrides` 得到本次执行超时；套用 `CancellationTokenSource.CancelAfter`
5) 错误映射：
   - 捕获所有异常 → 封装为 `ToolExecutionException(toolName, argsSummary, reason)` 并记录

### S6：可用性过滤（IsAvailable）

1) 在清单阶段与执行阶段各调用一次 `IsAvailable(ctx)`；任一为 false 则隐藏/拒绝
2) 典型实现：硬件存在/通电、世界状态满足、玩家权限等

### S7：执行入口（ExecuteToolAsync）

1) 读取工具/Schema/Meta → 可用性 → 参数校验 → 沙箱执行
2) 记录完整审计：开始/结束/失败/耗时/参数摘要（脱敏）
3) 返回执行结果对象

### S8：演示工具（DemoTools）

1) `GetColonyStatusTool`（只读）：
   - Schema：无参/或简单开关参数
   - 依赖 `IWorldDataService` 获取 Colony 概要数据（字符串或对象摘要）
   - Meta：`ReadOnly`、`HasSideEffects=false`、`DefaultTimeoutMs=2000`

示例（伪代码）：
```csharp
internal sealed class GetColonyStatusTool : IRimAITool {
  private readonly Modules.World.IWorldDataService _world;
  public GetColonyStatusTool(Modules.World.IWorldDataService world) { _world = world; }

  public string Name => "GetColonyStatus";
  public string Description => "获取殖民地资源/心情/威胁的简要概览";

  public ToolFunction GetSchema() => new ToolFunction {
    Name = Name,
    Description = Description,
    Parameters = new ToolParameters { /* 最小 JSON Schema */ }
  };

  public ToolMeta GetMeta() => new ToolMeta { Concurrency = ToolConcurrency.ReadOnly, HasSideEffects = false, DefaultTimeoutMs = 2000 };
  public bool IsAvailable(ToolContext ctx) => true; // 可接入硬件/通电检测

  public async Task<object> ExecuteAsync(Dictionary<string, object> args, ToolContext ctx, CancellationToken ct) {
    var player = await _world.GetPlayerNameAsync(ct);
    // 这里可组装简单摘要对象
    return new { player, summary = "OK" };
  }
}
```

### S9：日志与事件（ToolLogging）

1) 统一前缀：`[RimAI.P4.Tool]`；打印 Start/Finish/Fail，含 tool、origin、convId 哈希、耗时、rate/timeout 命中
2) 可选事件（当存在事件总线时）：`ToolDiscovered/ToolAvailabilityChanged/ToolExecuted/ToolFailed`

### S10：Debug 面板（P4_ToolExplorer / P4_ToolRunner）

1) 工具列表：搜索/过滤（可用/危险/只读/主线程/独占）
2) 详情视图：Schema/Meta/可用性实时状态
3) 运行器：基于 Schema 自动生成参数编辑表单 → 执行 → 输出结果/耗时/错误
4) 黑白名单管理：启用/禁用工具；危险工具（HasSideEffects）可显示确认对话（默认关闭）

### S11：DI 注册与启动检查

1) `ServiceContainer.Init()` 注册：`IToolRegistryService -> ToolRegistryService`
2) 启动：`ToolDiscovery` 扫描并构造 → 注册表加载 → 打印清单摘要（总数/禁用/可用）

### S12：边界自检与回归

1) 工具实现文件 grep 检查：不得 `using Verse`；不得使用 `CoreServices` 直接定位依赖
2) 压测：并发执行只读工具 N=100（线程池），确认无帧抖动；主线程工具执行有序

---

## 6. 验收 Gate（必须全绿）

- 发现/清单
  - 启动日志打印：已发现工具 ≥ 1；黑白名单正确生效
  - Debug 列表可见工具与 Schema/Meta
- 参数校验
  - 缺参/类型错 → 清晰 `validation_error`，工具不执行
- 并发与主线程
  - `RequiresMainThread` 工具在主线程执行（日志可证）；`Exclusive` 工具互斥（两次调用串行）
- 速率/超时
  - RateLimit 命中返回 `rate_limited`；设置 Timeout=500ms 的演示工具在 500ms 左右失败
- 可用性
  - `IsAvailable=false` 时，清单隐藏或执行拒绝（带原因）
- Debug 面板
  - 可搜索/过滤/查看详情/一键运行；结果与错误可录屏复现

---

## 7. 回归脚本（人工/录屏）

1) 打开 Debug → Tool Explorer：确认工具列表与 Schema/Meta 正确
2) 运行 `GetColonyStatusTool` 两次（冷/热），记录耗时（预期 < 200ms）
3) 构造缺参请求 → 应返回 `validation_error`
4) 将工具 Meta 临时改为 `RequiresMainThread` → 运行并观察主线程日志
5) 设置 `PerToolOverrides.TimeoutMs=500` → 运行并观察超时
6) 开启 RateLimit=1/min → 连续两次调用，第二次 `rate_limited`

---

## 8. CI/Grep Gate（必须通过）

- Verse 访问最小面：
  - 全仓 grep：`using\s+Verse` 不得出现在 `Modules/Tooling/**` 与 `DemoTools/**`
- 依赖纪律：
  - 全仓 grep：工具实现禁用 `CoreServices`/Service Locator；仅构造函数注入
- 契约一致性：
  - 全仓 grep：`IToolRegistryService`/`IRimAITool` 签名与本文一致（字段/方法/大小写）
- 性能预算：
  - 并发只读压测无持续帧抖动（可通过脚本/人工验证）

---

## 9. 风险与缓解

- 工具绕过沙箱 → 通过 CI/Grep Gate 禁止 Verse/ServiceLocator；审查工具依赖
- 速率/互斥过严导致卡顿 → 逐步放宽阈值；对只读工具放行并发
- 参数校验误报/漏报 → 保守策略：严格必填/类型，宽松枚举/范围并给出警告
- 危险工具误触发 → 默认隐藏/确认；仅在开发/Debug 中开启

---

## 10. FAQ（常见问题）

- Q：工具必须返回什么类型？
  - A：返回可序列化对象（字符串/匿名对象/POCO）。上游会在 P12 将其转为提示词片段或直接展示。
- Q：如何声明“快速直出”支持？
  - A：上游可传入保留参数 `__fastResponse=true`；工具可返回面向玩家的成品字符串作为直出文本。
- Q：工具能直接访问世界吗？
  - A：不行。只读通过 `IWorldDataService`，写操作通过 `ICommandService`（后续阶段）。

---

## 11. 变更记录（提交要求）

- 初版（v5-P4）：交付工具契约/注册/绑定/沙箱/Debug/CI Gate；不改对外 Contracts
- 后续修改：如需新增 Meta/Context 字段，需向后兼容并在本文“配置/步骤/Gate”同步更新

---

本文件为 V5 P4 唯一权威实施说明。实现与验收以本文为准。
