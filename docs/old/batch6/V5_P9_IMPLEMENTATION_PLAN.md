# RimAI V5 — P9 实施计划（Stage：薄仲裁与路由 + 插件化 Act/Trigger + Stage 专属日志）

> 目标：一次性交付“极薄的仲裁与路由层（Stage）”，统一注册/启停 Act 与 Trigger，进行资源仲裁（互斥/合流/冷却/幂等）、颁发与回收票据（lease）、调度运行获批的 Act，并在历史服务的“Stage 专属日志线程”写入每次 Act 的最终总结文本。本文档为唯一入口，无需查阅旧文即可落地 P9 并完成验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 薄 Stage 层：注册/启停 Act 与 Trigger；仲裁（互斥/合流/冷却/幂等/并发上限）；颁发/续租/回收票据；路由到 Act 执行；统一写入 Stage 专属日志；广播事件与调试查询。
  - 插件化 Act/Trigger：群聊 `GroupChatAct` 作为一个内置 Act（仅示例），可启停；新增 `AlphaFiberInterServerChatAct`（AI 服务器间对话，依据“Alpha 光纤”连接拓扑的专用 Act 与 Trigger）。
  - 历史聚合：在历史服务中开设“Stage 专属日志线程”，将所有 Act 的“最终总结（FinalText）”聚合记录于同一 convKey（参与者集合仅为 `agent:stage`）。
  - 可观测：Stage 事件模型与 Debug 面板（注册表/触发/票据/最近记录/一键释放票据）。

- 非目标（后续阶段处理）
  - 不实现 Stage 内的 Prompt 组装与 LLM 调用（由 Act 内部负责，且一律非流式）。
  - 不在 Stage 写常规会话历史（仅写入“Stage 专属日志线程”）。
  - 不引入厚 Stage（如多轮轮次、剧情 Act 的详细流程），Stage 只做“仲裁与路由”。
  - 不在本阶段扩展 UI 会话或 Orchestration 能力。

---

## 1. 架构总览（全局视角）

- 定位：Stage 是“极薄”的系统服务，仅负责 Act/Trigger 的注册启停、仲裁（互斥/合流/冷却/幂等）、颁发票据与路由执行、写入统一的 Stage 日志与事件广播。Stage 不直接访问 Verse/Framework，不组装 Prompt，不触达 LLM。

- 依赖边界（对齐 V5）
  - 只读世界数据：如需读取世界快照，Act/Trigger 通过 `IWorldDataService`（P3）在主线程安全访问。
  - LLM：Act 内部若需文本生成，统一经 `ILLMService`（P2）且非流式（后台/服务路径纪律）。
  - 历史：Stage 统一使用 `IHistoryService`（P8）仅向“Stage 专属日志线程”追加最终总结；Act 禁止直接写历史。
  - 配置：`IConfigurationService`（P1）读取 `CoreConfig.Stage` 及各 Act 子配置并支持热重载回调。

- 单一日志线程（强约束）
  - 约定稳定参与者 ID：`AgentStageId = "agent:stage"`（保留字），其 convKey 即为 `"agent:stage"`（单参与者集合的拼接）。
  - 一切 Act 在一次运行完成后产出的 `FinalText`，由 Stage 统一调用 `IHistoryService.AppendAiFinalAsync("agent:stage", finalText)` 写入日志。
  - 文本结构建议：首行 Header（Act/Origin/ConvKey/Rounds/Latency/Result），其后为 ≤ MaxFinalTextChars 的人类可读摘要。

---

## 2. 接口契约（内部 API）

> 下述接口位于 Core 内部命名空间，不进入 `RimAI.Core.Contracts` 稳定层。

```csharp
// 触发意图（Trigger → Stage）
internal sealed class StageIntent {
  public string ActName { get; init; }
  public IReadOnlyList<string> ParticipantIds { get; init; } // 参与者集合（用于互斥与 convKey 生成）
  public string Origin { get; init; } // PlayerUI|PawnBehavior|AIServer|AlphaFiber|Other
  public string ScenarioText { get; init; } // 可选：场景/主题摘要
  public int? Priority { get; init; }
  public string Locale { get; init; }
  public string Seed { get; init; }
}

// 仲裁票据（StageKernel → Stage/Act）
internal sealed class StageTicket {
  public string Id { get; init; }
  public string ConvKey { get; init; }
  public IReadOnlyList<string> ParticipantIds { get; init; }
  public DateTime ExpiresAtUtc { get; init; }
}

internal sealed class StageDecision {
  public string Outcome { get; init; } // Approve|Reject|Defer|Coalesced
  public string Reason { get; init; }  // TooFewParticipants|Cooling|Conflict|...
  public StageTicket Ticket { get; init; }
}

// Act 执行请求（Stage → Act）
internal sealed class StageExecutionRequest {
  public StageTicket Ticket { get; init; }
  public string ScenarioText { get; init; }
  public string Origin { get; init; }
  public string Locale { get; init; }
  public string Seed { get; init; }
}

// Act 执行结果（Act → Stage）
internal sealed class ActResult {
  public bool Completed { get; init; }
  public string Reason { get; init; }    // Completed|Timeout|Rejected|Aborted|NoEligibleTargets|Exception
  public string FinalText { get; init; } // 必填：写入 agent:stage 的最终总结
  public int Rounds { get; init; }
  public int LatencyMs { get; init; }
  public object Payload { get; init; }   // 可选：简要结构化诊断
}

// 仲裁内核（资源/合流/冷却/幂等/lease）
internal interface IStageKernel {
  bool TryReserve(ActResourceClaim claim, out StageTicket ticket); // 互斥 + 并发上限
  void ExtendLease(StageTicket ticket, TimeSpan ttl);
  void Release(StageTicket ticket);
  bool IsBusyByConvKey(string convKey);
  bool IsBusyByParticipant(string participantId);
  Task<bool> CoalesceWithinAsync(string convKey, int windowMs, Func<Task<bool>> leaderWork);
  bool IsInCooldown(string key); void SetCooldown(string key, TimeSpan cooldown);
  bool IdempotencyTryGet(string key, out ActResult result); void IdempotencySet(string key, ActResult result, TimeSpan ttl);
}

internal sealed class ActResourceClaim {
  public IReadOnlyList<string> ConvKeys { get; init; }      // 至少包含 primary convKey
  public IReadOnlyList<string> ParticipantIds { get; init; }
  public string MapId { get; init; }                         // 可选 map 级互斥
  public bool Exclusive { get; init; } = true;               // 缺省强互斥
}

// 插件化 Act（仅执行与 Eligibility）
internal interface IStageAct {
  string Name { get; }
  bool IsEligible(StageExecutionRequest req); // 如参与者数量/状态检查
  Task<ActResult> ExecuteAsync(StageExecutionRequest req, CancellationToken ct);
  Task OnEnableAsync(CancellationToken ct);
  Task OnDisableAsync(CancellationToken ct);
}

// 触发器（主动扫描/被动事件 → 提交 Intent）
internal interface IStageTrigger {
  string Name { get; }
  string TargetActName { get; }
  Task OnEnableAsync(CancellationToken ct);
  Task OnDisableAsync(CancellationToken ct);
  Task RunOnceAsync(Func<StageIntent, Task<StageDecision>> submit, CancellationToken ct); // 主动扫描
}

// Stage 薄门面
internal interface IStageService {
  void RegisterAct(IStageAct act); void UnregisterAct(string name);
  void EnableAct(string name); void DisableAct(string name); IReadOnlyList<string> ListActs();
  void RegisterTrigger(IStageTrigger trigger); void UnregisterTrigger(string name);
  void EnableTrigger(string name); void DisableTrigger(string name); IReadOnlyList<string> ListTriggers();

  // 提交意图并进行仲裁（供 Trigger 调用）
  Task<StageDecision> SubmitIntentAsync(StageIntent intent, CancellationToken ct);

  // Debug：直接执行某 Act（跳过仲裁，仅用于测试）
  Task<ActResult> StartAsync(string actName, StageExecutionRequest req, CancellationToken ct);

  IReadOnlyList<RunningActInfo> QueryRunning();
}

internal sealed class RunningActInfo {
  public string ActName { get; init; }
  public string ConvKey { get; init; }
  public IReadOnlyList<string> ParticipantIds { get; init; }
  public string TicketId { get; init; }
  public DateTime LeaseExpiresUtc { get; init; }
}
```

---

## 3. 目录结构与文件

```text
RimAI.Core/
  Source/
    Modules/
      Stage/
        IStageService.cs
        StageService.cs                // 注册/启停/仲裁/路由/日志/事件
        Kernel/
          IStageKernel.cs
          StageKernel.cs               // 互斥/合流/冷却/幂等/lease/诊断
        Models/
          StageModels.cs               // StageIntent/Decision/Ticket/ExecutionRequest/ActResult/RunningActInfo/ActResourceClaim
        Acts/
          GroupChatAct.cs              // 插件示例（非流式，一次性总结）
          AlphaFiberInterServerChatAct.cs
        Triggers/
          ProximityGroupChatTrigger.cs // 邻近度扫描触发群聊（可选最小实现）
          AlphaFiberLinkTrigger.cs     // 基于“Alpha 光纤”连接拓扑触发服务器对话
        History/
          StageHistorySink.cs          // 写入 agent:stage
        Diagnostics/
          StageLogging.cs              // 统一前缀日志 + 事件
    UI/DebugPanel/Parts/
      P9_StagePanel.cs                 // 注册表/启停/触发/票据/最近记录/一键释放
```

---

## 4. 配置（内部 CoreConfig.Stage）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

```json
{
  "Stage": {
    "CoalesceWindowMs": 300,
    "CooldownSeconds": 30,
    "MaxRunning": 4,
    "LeaseTtlMs": 10000,
    "IdempotencyTtlMs": 60000,
    "LocaleOverride": null,
    "History": { "StageLogConvKey": "agent:stage", "MaxFinalTextChars": 800, "HeaderEnabled": true },
    "Logging": { "PayloadPreviewChars": 200, "SlowActWarnMs": 5000 },
    "DisabledActs": [],
    "DisabledTriggers": [],
    "Acts": {
      "GroupChat": { "Rounds": 1, "MaxLatencyMsPerTurn": 8000, "SeedPolicy": "ConvKeyWithTick" },
      "AlphaFiberInterServerChat": { "ScanIntervalSeconds": 600, "Pairing": "Random", "MaxPairsPerScan": 1 }
    }
  }
}
```

---

## 5. 实施步骤（一步到位）

> 按 S1→S14 完成；每步可通过 Debug 面板或日志进行自检；无需查阅其他文档。

### S1：模型与接口落地
- 新建 `Stage/Models/StageModels.cs`：包含 `StageIntent/StageDecision/StageTicket/StageExecutionRequest/ActResult/RunningActInfo/ActResourceClaim`。
- 新建接口文件：`IStageKernel.cs`、`IStageAct.cs`、`IStageTrigger.cs`、`IStageService.cs`。

### S2：仲裁内核 `StageKernel`
- 能力：
  - 互斥：同一 `convKey` 强互斥；参与者集合相交互斥；可选 map 级互斥；全局并行上限 `MaxRunning`。
  - 合流：`CoalesceWithinAsync(convKey, CoalesceWindowMs, leaderWork)`；窗口内复用结果。
  - 冷却：对 `key = actName+convKey`（或仅 convKey）设 `CooldownSeconds`。
  - 幂等：`idempotencyKey = sha256(actName + convKey + scenario + seed)`，ttl=`IdempotencyTtlMs`；缓存 `ActResult`。
  - 票据：颁发/续租/回收；超时自动回收；运行清单可查询。
- 日志：互斥/合流/冷却/幂等命中率、运行中票据数、慢执行告警。

### S3：Stage 薄门面 `StageService`
- 注册/启停 Act 与 Trigger：
  - 启动：扫描或显式注册；基于配置 `DisabledActs/DisabledTriggers` 决定初始状态；调用各自 `OnEnableAsync`。
  - 启停变更：即时生效；变更写日志并广播事件。
- `SubmitIntentAsync`：
  - 生成 `convKey = join('|', sort(participantIds))`；检查参与者数量（最小 2；服务器对话允许 =2 或专用策略）。
  - 内核合流/冷却/互斥/幂等；批准则颁发 `StageTicket`。
  - 通过线程池路由到目标 Act 执行（见 S5）；返回 `StageDecision`。
- Debug：`StartAsync` 允许直接路由到指定 Act（禁止在发行构建暴露；仅 Debug 面板调用）。

### S4：Stage 专属日志 `StageHistorySink`
- 依赖 `IHistoryService`（P8）。
- 写入规则：
  - convKey=`agent:stage`；当 `HeaderEnabled=true` 时生成标准 Header 行；对 `FinalText` 裁剪至 `MaxFinalTextChars`；
  - 调用 `AppendAiFinalAsync("agent:stage", header + "\n" + body)`；
  - 失败时记录错误并重试一次；仍失败只记日志，不抛出至外层（避免阻塞 Stage）。

### S5：Act 执行路由与超时
- 路由后：
  - 调用 Act 的 `IsEligible`，不通过即返回 `ActResult { Completed=false, Reason=Rejected, FinalText="(被拒绝原因概要)" }`；
  - 包裹执行超时（各 Act 子配置或 `MaxLatencyMsPerTurn` 的总预算）；
  - 执行期间每 `LeaseTtlMs/2` 续租一次；异常捕获后转为 `ActResult.Completed=false`，`Reason=Exception` 并写摘要。
- 收尾：调用 `StageHistorySink` 写日志 → `StageKernel.Release(ticket)` → `SetCooldown`。

### S6：事件与日志
- 广播事件（统一前缀 `[RimAI.P9.Stage]`）：
  - `StageIntentAccepted/Rejected`（payload：act/participants/convKey/reason/coalesced）
  - `ActStarted/ActFinished`（payload：act/convKey/latency/result/seed）
- 诊断：近 N 次记录、命中率、慢执行列表；Debug 面板可读。

### S7：内置 Act — GroupChatAct（最小实现）
- 语义：对传入参与者集合做一次主题化“群聊总结”，非流式，产出 `FinalText`。
- 依赖：`ILLMService`（P2，非流式）/`IWorldDataService`（只读快照）/`IConfigurationService`（Rounds/MaxLatency/SeedPolicy）。
- 流程：
  1) 从 `StageExecutionRequest` 读取 `ScenarioText/Locale/Seed`；采样少量近况（只读）；
  2) 构造简提示（模板内置于 Act），调用 `ILLMService.GetResponseAsync`；
  3) 返回 `ActResult { Completed=true, FinalText=summary, Rounds=1, LatencyMs=... }`；失败/超时返回占位摘要。
- 禁止：写历史（由 Stage 统一写入）。

### S8：内置 Trigger — ProximityGroupChatTrigger（可选）
- 定时扫描（默认 300s）：
  - 通过 `IWorldDataService` 只读获取可对话小人及邻近关系；
  - 选出满足最小参与者=2 的集合、去重后生成 `StageIntent` 提交 Stage；
  - `MaxNewConversationsPerScan` 限制本轮新建数量；
  - 遵守 Stage 的合流/冷却策略，触发过多时自动降频。

### S9：内置 Act — AlphaFiberInterServerChatAct（AI 服务器间对话）
- 背景：世界物品“Alpha 光纤”连接多台 AI 服务器（Thing）。
- 依赖：`ILLMService`（非流式）、`IWorldDataService`（只读拓扑快照）。
- 流程：
  1) 接收 `ParticipantIds=[thing:<serverA>, thing:<serverB>]`；
  2) 收集两端服务器状态（温度/供电/负载/报警；若可得）构成简提示；
  3) 非流式生成“服务器间对话总结”（如诊断/节能/隐患），返回 `FinalText`；
  4) 资源：按服务器 thingId 互斥；map 级冷却。

### S10：内置 Trigger — AlphaFiberLinkTrigger
- 定时扫描拓扑（`ScanIntervalSeconds`）：
  - 调用 `IWorldDataService.GetAlphaFiberLinksAsync()`（见 S12 扩展）获取当前所有连线对；
  - 随机或按权重选择 1 对（`MaxPairsPerScan`），生成 `StageIntent { ActName=AlphaFiberInterServerChat, ParticipantIds=[serverA, serverB], Origin=AlphaFiber }`；
  - 提交 Stage，由 Stage 仲裁与路由。

### S11：Debug 面板 `P9_StagePanel`
- 展示：已注册 Acts/Triggers 与启停状态；运行中票据；最近 N 次执行摘要；Stage 日志最新 50 条；
- 操作：启停 Act/Trigger；“Run Active Triggers Once”；直接执行某 Act（传入 participants 与 scenario）；强制释放票据；清理幂等缓存。

### S12：`IWorldDataService` 扩展（最小只读）
- 扩展方法（内部 DTO，不暴露 Verse）：
  - `Task<IReadOnlyList<(string serverAId, string serverBId)>> GetAlphaFiberLinksAsync(CancellationToken ct)`
  - `Task<AiServerSnapshot> GetAiServerSnapshotAsync(string serverId, CancellationToken ct)` // 温度/供电/负载/告警（若可得）
- 实现细节：在 `WorldDataService` 内部以主线程安全方式访问 Verse 并映射为 POCO。

### S13：DI 注册与启动检查
- `ServiceContainer.Init()` 注册：`IStageKernel -> StageKernel`、`IStageService -> StageService`，并注册内置 Act/Trigger；
- 读取 `CoreConfig.Stage` 初始化启停状态；打印启动摘要：`[P9] Stage ready (acts=X,triggers=Y)`。

### S14：回归脚本与文档同步
- 按“回归脚本”章节执行/录屏；本文件作为唯一权威实施说明同步提交。

---

## 6. 验收 Gate（必须全绿）

- 基础行为
  - 触发 → 仲裁（合流/冷却/互斥/幂等） → 路由 → Act 执行 → 写入 `agent:stage`；
  - GroupChat 与 AlphaFiber 两个内置 Act 可启停/可被触发；
  - Debug 面板可列出注册表/运行中票据/最近记录并可人工触发。

- 纪律
  - Stage/Kernel/Service/Triggers 不得引用 `ILLMService`/`RimAI.Framework`；
  - Stage/Kernel/Service/Acts/Triggers 不得 `using Verse`（只允许 `WorldDataService` 内部）；
  - 历史统一由 `StageHistorySink` 写入 `agent:stage`，Act 禁止自行写历史。

- 可观测
  - 事件流包含 `StageIntentAccepted/Rejected` 与 `ActStarted/ActFinished`；
  - 日志含合流/冷却/幂等命中率与慢执行告警；
  - `agent:stage` 线程连续记录每次 Act 的最终总结（Header 可开关）。

- 性能
  - 主线程新增 ≤ 1 ms/帧（Stage 仅调度/UI 刷新）；
  - 全局并发遵守 `MaxRunning`；票据 lease 超时自动回收，无泄漏。

---

## 7. 回归脚本（人工/录屏）

1) 启动 → Debug 面板查看 Stage 启动摘要；
2) 启用 `ProximityGroupChatTrigger` → 触发 1 次群聊 → 验证：事件链、`agent:stage` 新增一条带 Header 的总结；
3) `CoalesceWindowMs=300` 内多次触发同一 `convKey` → 仅 1 次执行；
4) 在 `CooldownSeconds` 内重复触发 → 被拒绝/冷却命中；
5) 启用 `AlphaFiberLinkTrigger`（构造最小拓扑或测试桩）→ 触发服务器对话 → `agent:stage` 新增一条总结；
6) 强制释放票据 → 再触发 → 正常执行；
7) 禁用任一 Act/Trigger → 行为即时变化；
8) 断网/超时注入（Act 内部）→ `ActResult.Completed=false` 且 `FinalText` 为占位，日志/事件可见，`agent:stage` 仍有记录。

---

## 8. CI / Gate（使用 Cursor 内置工具，必须通过）

- 唯一触达面：
  - 全仓（排除 `Modules/LLM/**`）：检查=0: `using\s+RimAI\.Framework`；
  - Stage/Kernel/Service/Triggers：检查=0: `ILLMService`；
  - 除 `WorldDataService` 与 `Persistence/**` 外：检查=0: `\bScribe\.|using\s+Verse`。

- 纪律：
  - 禁止属性注入；构造函数注入；
  - 历史写入点唯一：`StageHistorySink`；
  - `agent:stage` 作为 `StageLogConvKey` 常量注入 `IParticipantIdService` 白名单（允许 `agent:stage` 为稳定 ID）。

---

## 9. 风险与缓解

- 触发风暴：合流 + 冷却 + 幂等三重防护；上限 `MaxRunning`；必要时全局节流。
- 票据泄漏/死锁：lease TTL + 定期续租 + 超时自动回收；强制释放票据 Debug 操作。
- 日志刷屏：`MaxFinalTextChars` 裁剪；可关闭 Header；冷却窗口适当增大。
- 世界拓扑读取成本：AlphaFiber 触发器扫描周期可调；必要时 map 级缓存与增量变更监听。
- 语义分散：统一“Act 内非流式、Stage 不做业务”的文档与 CI 约束，避免回退到厚 Stage。

---

## 10. FAQ（常见问题）

- Q：为什么把所有 Act 总结写到同一个历史线程？
  - A：统一可观测与审计，避免散落在各会话导致无法总览；快速定位“系统级自动行为”。

- Q：Act 能写常规历史吗？
  - A：不能。Stage 模式下，Act 只返回 `FinalText`，由 Stage 统一写入专属日志线程。

- Q：群聊不逐条发言而是“总结”会不会丢细节？
  - A：P9 的目标是“组织者级事件审计”。逐条发言与轮次可在未来专用 Act 扩展，不应在 Stage 层实现。

- Q：Alpha 光纤拓扑来自哪里？
  - A：通过 `IWorldDataService` 的扩展方法 `GetAlphaFiberLinksAsync` 与 `GetAiServerSnapshotAsync` 仅只读采集，避免 Stage/Act 直接触达 Verse。

---

## 11. 变更记录（提交要求）

- 初版（v5-P9）：交付薄 Stage（仲裁/路由/日志）、内置 GroupChat/AlphaFiber 两个 Act 与对应 Trigger、Debug 面板、CI/Gate（Cursor 内置工具）与回归脚本。

---

## 附录 A：Mermaid 时序图（触发 → 仲裁 → 执行 → 日志）

```mermaid
sequenceDiagram
  autonumber
  participant Trig as StageTrigger
  participant Stage as StageService
  participant Kern as StageKernel
  participant Act as IStageAct
  participant Hist as StageHistorySink

  Trig->>Stage: SubmitIntent(intent)
  Stage->>Kern: Coalesce/Cooldown/Reserve
  alt 批准
    Kern-->>Stage: StageTicket
    Stage->>Act: ExecuteAsync(request{ticket,scenario})
    Act-->>Stage: ActResult{Completed,FinalText,Latency}
    Stage->>Hist: AppendAiFinal(agent:stage, header+summary)
    Stage->>Kern: Release(ticket); SetCooldown
  else 拒绝/合流
    Kern-->>Stage: StageDecision{Rejected|Coalesced}
  end
```

---

## 附录 B：Stage 日志文本规范（示例）

```
[Act=AlphaFiberInterServerChat][Origin=AlphaFiber][ConvKey=thing:serverA|thing:serverB][Latency=1420ms][Result=Completed]
服务器 A 与 B 的链路检查完成：
- A 温度 38°C，负载 62%；B 温度 36°C，负载 55%；
- 建议：夜间启用节能模式；下个象限更换 A 的冷却风扇。
```

---

## 附录 C：内置 Act 模板要点（非流式）

- GroupChatAct 最小提示：`locale`、参与者显示名、(可选) ScenarioText；产出 150–300 字总结。
- AlphaFiberInterServerChatAct 最小提示：服务器名/温度/供电/负载/报警 + ScenarioText；产出 150–300 字总结。
- 失败/超时占位：`"（本轮对话失败或超时，已跳过）"`。


