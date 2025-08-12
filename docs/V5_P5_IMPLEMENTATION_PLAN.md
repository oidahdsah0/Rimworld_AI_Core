# RimAI V5 — P5 实施计划（Orchestration-Min：工具仅编排）

> 目标：仅负责“工具链”的暴露、决策与执行，不做最终自然语言汇总；模式仅有 `Classic` 与 `NarrowTopK`，且由上游显式选择，**不进行任何自动切换或降级**。返回值仅为结构化的 Tool Calls 结果（JSON/DTO）。本文档为唯一入口文档，无需翻阅旧文即可落地与验收。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 编排入口 `IOrchestrationService.ExecuteAsync(...)`（工具仅编排）
  - 两种模式：`Classic`、`NarrowTopK`（固定选择，无自动降级）
  - LLM 决策 Tool Calls（一次非流式调用）→ 调用工具注册表执行 → 返回结构化结果
  - Debug 面板：Classic/NarrowTopK 一键测试，原样 JSON 展示

- 非目标（后续阶段处理）
  - 最终自然语言总结/复述（交由上游 Organizer/Prompt/ILLMService）
  - 多轮 Planner/递归（如需，后续独立引入）
  - 自动模式选择/降级链（禁止，完全由上游显式选择）

---

## 1. 架构总览（全局视角）

- 边界与原则
  - 由上游（UI/Stage/Debug/Organizer）以参数选择模式；**编排层不得进行任何自动切换/降级**
  - Classic：暴露可用工具全集给 LLM 进行 Function Calling 决策
  - NarrowTopK：先进行 TopK 候选缩减，再暴露给 LLM 决策；若缺失检索能力或候选为空 → 直接返回错误，不执行任何降级
  - 返回值仅 `ToolCallsResult`，不做历史写入、不做 LLM 总结

- 依赖
  - `IToolRegistryService`（P4）：获取清单、执行工具
  - `ILLMService`（P2）：非流式决策 Tool Calls（必须含会话 ID）
  - 可选 `IToolVectorIndexService`（若存在，用于 NarrowTopK 候选缩减；若不存在则报错）
  - `IConfigurationService`：读取最小编排配置（默认模式、TopK、预算上限等）

---

## 2. 接口契约（内部 API）

> 下述接口位于 Core 内部，不进入 Contracts 稳定层；与前文 P2/P4 的接口保持兼容。

```csharp
// RimAI.Core/Source/Modules/Orchestration/IOrchestrationService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RimAI.Core.Modules.Orchestration {
  internal enum OrchestrationMode { Classic, NarrowTopK }

  internal sealed class ToolOrchestrationOptions {
    public int MaxCalls { get; init; } = 1;            // LLM 决策允许的 ToolCalls 数上限
    public int NarrowTopK { get; init; } = 5;          // NarrowTopK 模式下暴露给 LLM 的工具数
    public double? MinScoreThreshold { get; init; }    // NarrowTopK 模式下可选阈值
    public string Locale { get; init; }                // 可选提示语言
  }

  internal sealed class ToolCallRecord {
    public string ToolName { get; init; }
    public Dictionary<string, object> Args { get; init; }
  }

  internal sealed class ToolExecutionRecord {
    public string ToolName { get; init; }
    public Dictionary<string, object> Args { get; init; }
    public string Outcome { get; init; }      // success | validation_error | unavailable | rate_limited | timeout | exception
    public object ResultObject { get; init; } // 序列化友好的返回对象
    public int LatencyMs { get; init; }
  }

  internal sealed class ToolCallsResult {
    public OrchestrationMode Mode { get; init; }
    public IReadOnlyList<string> ExposedTools { get; init; }          // 暴露给 LLM 的工具名
    public IReadOnlyList<ToolCallRecord> DecidedCalls { get; init; }  // LLM 决策出的调用（已截断至 MaxCalls）
    public IReadOnlyList<ToolExecutionRecord> Executions { get; init; } // 实际执行记录
    public bool IsSuccess { get; init; }          // 编排是否完成（不代表业务成功）
    public string Error { get; init; }            // 结构化错误摘要（如 no_tool_calls / narrow_topk_unavailable / no_candidates / invalid_args）
    public int TotalLatencyMs { get; init; }
  }

  internal interface IOrchestrationService {
    Task<ToolCallsResult> ExecuteAsync(
      string userInput,
      IReadOnlyList<string> participantIds,
      OrchestrationMode mode,
      ToolOrchestrationOptions options = null,
      CancellationToken ct = default);
  }
}
```

说明：
- `ExecuteAsync` 为唯一入口；`mode` 必填且固定本次流程，严禁在内部改变
- `ToolCallsResult` 为最终产物；上游自行决定是否拿结果再去做 LLM 汇总

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Orchestration/
        IOrchestrationService.cs
        OrchestrationService.cs       // 薄入口：解析 mode → 委托给对应实现
        Modes/
          IToolMatchMode.cs           // 模式统一接口
          ClassicMode.cs              // 暴露全集
          NarrowTopKMode.cs           // TopK 候选缩减（无检索则返回错误）
        OrchestrationLogging.cs       // 日志/事件帮助器
      Embedding/
        IToolVectorIndexService.cs    // 若已存在则引用；否则占位不实现（P9 提供）
```

---

## 4. 配置（内部 CoreConfig.Orchestration — 最小集）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

建议默认值：

```json
{
  "Orchestration": {
    "DefaultMode": "Classic",
    "Budget": { "MaxCalls": 1 },
    "NarrowTopK": { "TopK": 5, "MinScoreThreshold": 0.0 }
  }
}
```

说明：
- `DefaultMode` 仅作为 UI/Debug 默认值；调用方必须显式传入 `mode`
- `NarrowTopK` 仅在该模式时被消费

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S10；全程确保“无自动降级/无自动切换”。

### S1：定义接口与 DTO

1) 创建 `IOrchestrationService`、`ToolOrchestrationOptions`、`ToolCallRecord`、`ToolExecutionRecord`、`ToolCallsResult`、`OrchestrationMode`
2) 创建 `IToolMatchMode`（统一签名）

```csharp
internal interface IToolMatchMode {
  OrchestrationMode Mode { get; }
  Task<ToolCallsResult> RunAsync(string userInput, IReadOnlyList<string> participantIds, ToolOrchestrationOptions options, CancellationToken ct);
}
```

### S2：实现薄入口（OrchestrationService）

1) 依赖注入：`IEnumerable<IToolMatchMode>`、`IConfigurationService`
2) `ExecuteAsync(...)`：按入参 `mode` 选择具体实现；**不得读取配置改写模式**
3) 捕获实现抛出的异常 → 转为 `ToolCallsResult.IsSuccess=false` + `Error` 字段

### S3：ClassicMode 实现

1) 获取可用工具清单（`IToolRegistryService.GetAllToolSchemas`，按 Origin/黑白名单/`IsAvailable` 过滤）
2) 构造 `UnifiedChatRequest`：
   - 消息：`system`（指示仅返回 Function Calling）+ `user`（`userInput`）
   - Tools：暴露“全集”
   - 必须含 `ConversationId`
3) 调用 `ILLMService.GetResponseAsync`（非流式）→ 解析 Tool Calls；按 `MaxCalls` 截断；非法项标记 `invalid_args`
4) 依次执行 `IToolRegistryService.ExecuteToolAsync`（顺序；P5 默认串行）→ 记录 `ToolExecutionRecord`
5) 汇总 `ToolCallsResult` 返回（不做 LLM 总结）

### S4：NarrowTopKMode 实现（无降级）

1) 检查是否存在 `IToolVectorIndexService`（或等价 TopK 能力）：
   - 若不存在 → 直接返回 `{ IsSuccess:false, Error:"narrow_topk_unavailable" }`
2) 获取可用工具清单；使用索引服务对“name+description+parameters 摘要”检索 TopK（受 `MinScoreThreshold` 影响）
3) 若 TopK 结果为空 → 返回 `{ IsSuccess:false, Error:"no_candidates" }`
4) 构造请求与执行流程与 Classic 相同，但 Tools 仅为 TopK 候选集

### S5：请求与提示约束

1) 固定系统提示：要求“仅通过 function_call 返回调用与参数”；禁止自然语言
2) 不注入历史/世界数据（由上游控制）；仅插入 Tools 定义与必要元数据
3) `Locale`：可选设置影响系统提示语言

### S6：错误与边界处理（无降级）

- LLM 未返回 Tool Calls → `no_tool_calls`
- Tool 名称不存在或参数校验失败 → `invalid_args`（逐项记录 outcome）
- 工具执行失败（不可用/速率/超时/异常）→ 对应 outcome；不影响其它项
- NarrowTopK 检索缺失/候选为空 → `narrow_topk_unavailable` / `no_candidates`；**不得回退 Classic**

### S7：日志与事件（可选）

- 统一前缀：`[RimAI.P5.Orch]`；打印：暴露工具数/TopK/决策条数/各 outcome 统计/耗时
- 进度事件（可选）：`PrepareTools` → `LLMDecision` → `ExecuteTool(n)` → `Finished`

### S8：Debug 面板（两按钮）

- `Orchestration_Classic_Test`：输入文本 → 展示 `ToolCallsResult` 原样 JSON（ExposedTools/DecidedCalls/Executions）
- `Orchestration_NarrowTopK_Test`：同上；若无索引服务或候选空，清晰显示错误字段

### S9：DI 注册与启动检查

1) `ServiceContainer.Init()` 注册：
   - `IOrchestrationService -> OrchestrationService`
   - `IToolMatchMode -> ClassicMode`、`IToolMatchMode -> NarrowTopKMode`
2) 启动打印：`[P5] Orchestration ready (modes=2)`

### S10：边界自检与回归

- 自检：
  - grep=0：`\bAuto\b|degrad|fallback`（确保无自动切换/降级字样）
  - Orchestration 不引用 `IPromptService`/`Prompt*`；不做汇总
  - Orchestration 不使用流式 LLM 接口

---

## 6. 验收 Gate（必须全绿）

- Classic
  - 能暴露全集；LLM 返回 ≥1 条合法 Tool Call；执行成功返回结构化结果
  - 未返回 Tool Calls 时，`Error = "no_tool_calls"`
- NarrowTopK
  - 有索引服务时，仅暴露 TopK 并决策成功；TopK/阈值可控
  - 无索引服务或候选空：`Error = "narrow_topk_unavailable"` 或 `"no_candidates"`（不回退 Classic）
- 一致性
  - 返回仅 `ToolCallsResult`（无自然语言）；Debug 面板展示原样 JSON
- 纪律
  - 全仓 `grep` 确认无“Auto/降级/回退”相关字符串；Orchestration 目录无 `IPromptService` 引用

---

## 7. 回归脚本（人工/录屏）

1) Classic：输入“殖民地概况” → 预期返回 1 条工具调用并执行，JSON 展示 `Executions[0].Outcome = success`
2) Classic 异常：输入故意触发无工具情形 → `Error = no_tool_calls`
3) NarrowTopK（有索引）：同 1)，但 `ExposedTools` 长度 = TopK；日志打印 TopK 信息
4) NarrowTopK（无索引）：按钮直接返回 `narrow_topk_unavailable`
5) 无汇总验证：上游不调用任何汇总，页面仅显示 JSON 结果

---

## 8. CI/Grep Gate（必须通过）

- 无自动切换/降级：
  - `grep -R "\bAuto\b\|degrad\|fallback" RimAI.Core/Source/Modules/Orchestration | cat` → 空
- 目录依赖限制：
  - Orchestration 目录 grep=0：`IPromptService|PromptAssembly|PromptComposer|StreamResponseAsync\(`
- 契约与实现一致性：
  - `IOrchestrationService.ExecuteAsync` 签名与本文一致；`IToolMatchMode` 存在两类实现

---

## 9. 风险与缓解

- 索引服务缺失导致 NarrowTopK 常不可用 → 用文案明确告知，允许上游在 UI 层屏蔽该模式
- LLM 决策返回空/非法 → 保守返回 `no_tool_calls`/`invalid_args`；上游决定下一步（如直接提示或改走其它入口）
- 并发与时延 → 工具执行仍由 P4 沙箱治理；P5 默认串行，后续按需开启只读小并发

---

## 10. FAQ（常见问题）

- Q：为什么不在编排层做最终汇总？
  - A：职责内聚与可组合性更佳；上游可按需调用 Organizer/Prompt/ILLMService 完成汇总。
- Q：为何不做自动降级？
  - A：决策权交给上游，避免“隐式行为”；可观测性与可控性更强。
- Q：NarrowTopK 必须依赖索引吗？
  - A：是。若缺失索引服务（或检索组件），本模式直接不可用，由上游决定是否选择 Classic。

---

## 11. 变更记录（提交要求）

- 初版（v5-P5）：交付编排入口 + Classic/NarrowTopK 两模式 + Debug/CI Gate；不改对外 Contracts
- 后续修改：如需新增模式或 Planner，请在本文“范围/步骤/Gate”同步更新，并保持“无自动切换/无降级”原则

---

本文件为 V5 P5 唯一权威实施说明。实现与验收以本文为准。
