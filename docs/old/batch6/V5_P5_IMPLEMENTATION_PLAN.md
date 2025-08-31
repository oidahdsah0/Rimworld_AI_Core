# RimAI V5 — P5 实施计划（Orchestration-Min：工具仅编排，消费 Tool Service 的 Tool JSON）

> 目标：编排层仅负责“工具链”的暴露、决策与执行，不做任何向量检索与最终汇总；模式仅有 `Classic` 与 `NarrowTopK`，由上游显式选择，**无自动切换/无自动降级**。编排层统一向 Tool Service 请求 Framework Tool Calls 所需的 Tool JSON（Classic=全集；NarrowTopK=TopK+分数表），再通过 LLM 决策一次 Tool Calls 并交由注册表执行。为未来“工具链（多次调用）/并发只读调用”预留数据结构与参数（本阶段不实现）。本文档为唯一入口，无需旧文。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - `IOrchestrationService.ExecuteAsync(...)` 作为唯一入口（工具仅编排）
  - 两种工具曝光模式：`Classic`、`NarrowTopK`（固定选择，编排层不处理向量，也不降级）
  - 一次 LLM 非流式决策 Tool Calls → 逐条执行并返回结构化结果
  - 新增“执行档位（ExecutionProfile）”输入参数：`Fast`（实现）、`Deep`（预留，当前不实现）
  - Debug 面板：Classic/NarrowTopK 测试按钮，原样 JSON 展示

- 非目标
  - 工具向量化/索引/检索（由 P4 Tool Service 内部负责）
  - 最终自然语言汇总/历史写入/多轮 Planner（后续阶段）
  - 任何“Auto/回退/降级”逻辑（禁止）
  - 工具链与并发执行（仅预留结构，不在 P5 实现）

---

## 1. 架构总览（全局视角）

- 依赖
  - `IToolRegistryService`（P4）：
    - `GetClassicToolCallSchema(...)` → 全集 Tool JSON
    - `GetNarrowTopKToolCallSchemaAsync(input,k,threshold,...)` → TopK Tool JSON + 分数表（索引未就绪/无候选即抛错）
    - `ExecuteToolAsync(...)` → 执行工具
  - `ILLMService`（P2）：非流式获取一次 Tool Calls 决策（需 `ConversationId`）
  - `IConfigurationService`（P1）：读取最小预算/默认值

- 原则
  - 编排层不调用 Embedding，不维护任何向量状态
  - 模式由上游固定；NarrowTopK 失败（索引未就绪/无候选）直接返回错误，不回退 Classic
  - 返回 `ToolCallsResult`，不做汇总
  - 仅需“一句 prompt（userInput）+ 必要上下文（participantIds/convId）”即可进入工具匹配

---

## 2. 接口契约（内部 API）

> 新增执行档位 `ExecutionProfile`；为“工具链/并发”预留字段（当前未实现）。

```csharp
namespace RimAI.Core.Modules.Orchestration {
  internal enum OrchestrationMode { Classic, NarrowTopK }
  internal enum ExecutionProfile { Fast, Deep } // Deep 预留：多轮/并发/链式能力，不在 P5 实现

  internal sealed class ToolOrchestrationOptions {
    public OrchestrationMode Mode { get; init; }               // 为便于上游传参，也可放入方法入参
    public ExecutionProfile Profile { get; init; } = ExecutionProfile.Fast;

    public int MaxCalls { get; init; } = 1;                    // LLM 单次决策允许的 ToolCalls 上限
    public int NarrowTopK { get; init; } = 5;                  // NarrowTopK 模式下 TopK
    public double? MinScoreThreshold { get; init; }
    public string Locale { get; init; }

    // 预留：并发只读/最大并行度（当前 Fast 档位忽略）
    public bool AllowParallelReadOnly { get; init; } = false;  // 预留
    public int MaxParallelism { get; init; } = 1;              // 预留
  }

  internal sealed class ToolCallRecord {
    public string CallId { get; init; }                        // 预留：调用ID
    public string ToolName { get; init; }
    public Dictionary<string, object> Args { get; init; }

    // 预留：链路与并发分组（当前不使用）
    public string GroupId { get; init; }                       // 并发/批次分组
    public int Order { get; init; }                            // 顺序执行时的次序
    public IReadOnlyList<string> DependsOn { get; init; }      // 依赖的 CallId 列表
  }

  internal sealed class ToolExecutionRecord {
    public string CallId { get; init; }
    public string GroupId { get; init; }
    public string ToolName { get; init; }
    public Dictionary<string, object> Args { get; init; }
    public string Outcome { get; init; }      // success | validation_error | unavailable | rate_limited | timeout | exception
    public object ResultObject { get; init; }
    public int LatencyMs { get; init; }

    // 预留：并发/多轮可观测
    public int Attempt { get; init; }                         // 重试/并发尝试编号
    public DateTime StartedAtUtc { get; init; }
    public DateTime FinishedAtUtc { get; init; }
  }

  internal sealed class ToolCallsResult {
    public OrchestrationMode Mode { get; init; }
    public ExecutionProfile Profile { get; init; }

    public IReadOnlyList<string> ExposedTools { get; init; }          // 本轮暴露的工具名
    public IReadOnlyList<ToolCallRecord> DecidedCalls { get; init; }  // LLM 决策
    public IReadOnlyList<ToolExecutionRecord> Executions { get; init; }

    public bool IsSuccess { get; init; }
    public string Error { get; init; }                                // 如 no_tool_calls / index_not_ready / no_candidates / invalid_args / profile_not_implemented
    public int TotalLatencyMs { get; init; }

    // 预留：将来链路追踪/解释
    public IReadOnlyList<string> PlanTrace { get; init; }             // 预留：步骤轨迹（P12+）
  }

  internal interface IOrchestrationService {
    Task<ToolCallsResult> ExecuteAsync(
      string userInput,
      IReadOnlyList<string> participantIds,
      ToolOrchestrationOptions options,     // 将 Mode/Profile/TopK 等集中入参
      CancellationToken ct = default);
  }
}
```

说明：
- 当前仅实现 `Profile = Fast`：一次 LLM 决策 + 串行执行；并发/链式字段为将来预留
- 兼容性：若传入 `Profile = Deep`或者`Profile = Wide`，直接返回 `profile_not_implemented` 错误

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Orchestration/
        IOrchestrationService.cs
        OrchestrationService.cs
        Modes/
          IToolMatchMode.cs
          ClassicMode.cs            // Tool Service → 全集 Tool JSON
          NarrowTopKMode.cs         // Tool Service → TopK Tool JSON + 分数表
        OrchestrationLogging.cs
```

---

## 4. 配置（内部 CoreConfig.Orchestration — 最小）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

```json
{
  "Orchestration": {
    "DefaultMode": "Classic",
    "Budget": { "MaxCalls": 1 },
    "NarrowTopK": { "TopK": 5, "MinScoreThreshold": 0.0 },
    "DefaultProfile": "Fast"   // 新增默认执行档位；调用方仍需显式传入
  }
}
```

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S10；确保“编排层不做向量、不做降级；仅实现 Fast 档位”。

### S1：接口与薄入口

- 定义 `ExecutionProfile`；在 `ToolOrchestrationOptions` 中新增 `Profile`
- 将 `mode` 并入 `ToolOrchestrationOptions.Mode`，或保留为方法入参（二选一，以代码实现为准）
- `OrchestrationService.ExecuteAsync`：
  - 若 `Profile=Deep` → 立即返回 `profile_not_implemented`
  - 若 `Profile=Wide` → 立即返回 `profile_not_implemented`
  - 否则按 `Mode` 选择 `ClassicMode|NarrowTopKMode`

### S2：ClassicMode（Fast 档位）

1) 向 Tool Service 请求全集 Tool JSON
2) 构造 `UnifiedChatRequest`（system 限制仅返回 function_call；user=userInput；tools=Tool JSON；必须含 convId）
3) 调用 `ILLMService.GetResponseAsync` → 解析 Tool Calls（截断至 `MaxCalls`）
4) 逐条 `ExecuteToolAsync`（串行）→ 记录 `ToolExecutionRecord`

### S3：NarrowTopKMode（Fast 档位）

1) 向 Tool Service 请求 TopK Tool JSON + 分数表
   - 索引未就绪/构建中/无候选 → 捕获并返回 `index_not_ready/index_building/no_candidates`
2) 后续同 Classic，但仅暴露 TopK 工具；日志附分数摘要

### S4：请求与提示约束

- 系统提示固定“仅通过 function_call 返回 Tool Calls”
- 不注入历史/世界数据；Locale 可影响提示语言

### S5：错误与边界（无降级）

- 无 Tool Calls → `no_tool_calls`
- 非法名称/参数 → `invalid_args`
- 工具执行失败 → 对应 outcome，继续其它项
- `Profile=Deep` → `profile_not_implemented`
- `Profile=Wide` → `profile_not_implemented`

### S6：日志与事件

- 前缀 `[RimAI.P5.Orch]`；记录模式/档位/暴露工具数/TopK 分数摘要/决策条数/各 outcome/总耗时

### S7：Debug 面板

- Classic/NarrowTopK 两按钮；新增“ExecutionProfile”选择器（Fast/Deep），Deep 返回 `profile_not_implemented`

### S8：DI 注册与启动检查

- 注册 `IOrchestrationService`、两个 `IToolMatchMode`；启动日志显示 `modes=2, profile=Fast`

### S9：边界自检（grep）

- Orchestration 目录不得出现 `GetEmbeddingsAsync|IPromptService|Prompt|StreamResponseAsync(`
- grep 0：`\bAuto\b|degrad|fallback`

### S10：回归脚本

1) Classic/Fast：正常返回并执行
2) NarrowTopK/Fast：索引就绪返回 TopK；未就绪/构建中/无候选返回错误
3) 任一模式/Deep、Wide：返回 `profile_not_implemented`

---

## 6. 验收 Gate（必须全绿）

- 仅“一句 prompt + 必要上下文”即可进入工具匹配（Classic/NarrowTopK）
- `Profile=Fast` 可用；`Profile=Deep` 返回明确错误
- 无自动降级/回退；Orchestration 不做向量/不做汇总

---

## 7. 风险与缓解

- 调用方误以为 Deep 已支持 → UI/日志明确提示 `profile_not_implemented`
- 结果为空 → 交由上游决定下一步（提示玩家/换模式/重试）

---

## 8. 变更记录

- v5-P5（调整）：新增 `ExecutionProfile`（Fast/Deep）；预留链式/并发数据结构；当前仅实现 Fast；编排仍消费 Tool Service 的 Tool JSON。

---

本文件为 V5 P5 唯一权威实施说明。实现与验收以本文为准。

---

## 9. CI / Gate（使用 Cursor 内置工具，必须通过）

- 编排层不做向量与降级：
  - 检查=0：`\bGetEmbeddingsAsync\b|\bIPromptService\b|\bPrompt\b|StreamResponseAsync\(`
- 仅消费 Tool Service：
  - `Modules/Orchestration/**` 中不得 `using\s+RimAI\.Framework`；只允许经 `IToolRegistryService` 取得 Tool JSON。
- 非流式纪律：后台路径 检查=0：`StreamResponseAsync\(`。
- 注入纪律：仅构造函数注入；禁止属性注入。
