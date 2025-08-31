# RimAI V5 — P12 实施计划（ChatUI 指令模式：编排→RAG→流式回答）

> 最新注意事项（Debug 页面移除）
>
> - Debug 页面（DebugPanel/Debug Window）已从项目中移除，后续不再需要。
> - 文中若提及 Debug/面板，仅为历史参考，不作为必须项或 Gate 依赖。
> - 流式范围：仅 ChatWindow UI（以及 UI 摘要区，如存在）允许流式展示；后台/服务路径一律非流式。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 定义 ChatUI 的“指令模式”两段式流程：编排（非流式）→ RAG 合并 → LLM 真流式回答（仅 UI）。
  - 编排服务返回结构化结果的同时，产出“过程文案（PlanTrace）”，并将其写入历史（History）。
  - 为工具提供“显示名 DisplayName”（面向玩家的短名称，支持本地化）。
  - Prompting 支持合并“工具结果”到系统提示或上下文块（RAG）后再发起流式请求。

- 非目标（本阶段不做）
  - Deep/Wide Profile（不实现）。
  - 工具链/并发执行（仍为后续能力）。
  - 任意自动降级/回退（禁止）。

---

## 1. 目标

- ChatUI 的“命令”请求应：
  1) 将用户提示词交给编排服务；
  2) 编排服务按既定路径匹配并执行工具，产出结构化结果与过程文案（PlanTrace）；
  3) ChatUI 将“工具结果”作为 RAG 提示合入 Prompt，再对 LLM 发起一次真流式请求并展示；
  4) PlanTrace 必须写入历史；取消/指示灯等仅包围第 3 步的流式过程。

---

## 2. 总体流程（两段式）

1) 段1（后台非流式：编排）
   - 输入：`userInput`、`participantIds`、`ToolOrchestrationOptions(Profile=Fast, Mode=Classic|NarrowTopK, MaxCalls=1)`。
   - 流程：
     - 从 Tool Service 获取工具 JSON（Classic=全集；NarrowTopK=TopK+分数表）。
     - 构造 system+历史+本次用户输入的 Messages 列表；调用 LLM（非流式，带工具）进行一次 function-calling 决策。
     - 解析 tool_calls（取首个），串行执行工具，得到结构化结果对象。
     - 通过“编排叙述器（OrchestrationNarrator）”生成过程文案 `PlanTrace`，并写入历史（AI 角色、非最终回合，不推进 TurnOrdinal）。
     - 返回 `ToolCallsResult`（含 ExposedTools、DecidedCalls、Executions、PlanTrace、HitDisplayName 等）。

2) 段2（UI 真流式：RAG→LLM）
   - ChatUI 将“工具结构化结果”转为 RAG 块（上下文块或系统段），合入 Prompt；
   - 使用 `_llm.StreamResponseAsync(UnifiedChatRequest)` 发起真流式请求，仅在 UI 渲染分片；
   - 取消、指示灯等仅在此段生效；完成后仅将“用户最后一条 + AI 最终文本”写入历史。

备注：若编排无命中工具，PlanTrace 为空，RAG 不注入工具结果；仍可直连 LLM 生成自然语言回答。

---

## 3. 接口契约（内部 API 变更）

### 3.1 工具接口 `IRimAITool`

新增字段：

```csharp
internal interface IRimAITool {
    string Name { get; }
    string Description { get; }
    string ParametersJson { get; } // JSON Schema
    string DisplayName { get; }     // 新增：给玩家看的短名称，本地化优先
    string BuildToolJson();
}
```

要求：
- Demo 工具补齐 `DisplayName`（暂提供 zh-Hans 内置文案，后续支持资源化）。

### 3.2 编排返回 `ToolCallsResult`

在现有属性基础上补充约定：

```csharp
internal sealed class ToolCallsResult {
    public OrchestrationMode Mode { get; set; }
    public ExecutionProfile Profile { get; set; }
    public IReadOnlyList<string> ExposedTools { get; set; }
    public IReadOnlyList<ToolCallRecord> DecidedCalls { get; set; }
    public IReadOnlyList<ToolExecutionRecord> Executions { get; set; }
    public bool IsSuccess { get; set; }
    public string Error { get; set; }
    public int TotalLatencyMs { get; set; }
    public IReadOnlyList<string> PlanTrace { get; set; } // 本阶段用于“过程文案”，必须非空时写入历史

    // 建议新增：命中工具的显示名（若无命中为空字符串）
    public string HitDisplayName { get; set; }
}
```

说明：
- `PlanTrace`：当前仅包含一条人话文案；留作将来扩展多步轨迹。
- `HitDisplayName`：命中工具的 `DisplayName`，用于模板化生成 `PlanTrace` 与 UI 展示。

### 3.3 历史服务 `IHistoryService`

为支持“过程文案写历史但不推进回合”，新增接口：

```csharp
// 新增：写入 AI 过程说明（不推进回合，不参与 Recap 的 Turn 分桶）
Task AppendAiNoteAsync(string convKey, string text, CancellationToken ct = default);
```

实现要点：
- 存储为 `EntryRole.Ai`，但 `TurnOrdinal = null`；保留时间戳与删除编辑功能。
- 现有 Recap 逻辑仅统计 `TurnOrdinal.HasValue` 的 AI 条目，因而不会将过程文案纳入摘要窗口。

---

## 4. 编排叙述器（OrchestrationNarrator）

职责：根据命中工具显示名与参与者，生成本地化的过程文案。

签名示例：

```csharp
internal static class OrchestrationNarrator {
    public static string FormatPlanTrace(
        string locale,
        IReadOnlyList<string> participantIds,
        string hitDisplayName,
        string pawnDisplayNameFallback = "Pawn")
    { /* 返回一条文案 */ }
}
```

模板（示例，zh-Hans）：
- `"{Pawn}从随身设备中找到了合适的APP：{displayName}，软件已经显示结果。"`

命名解析：
- 若会话含 `pawn:<loadId>`，可通过 `IWorldDataService` 获取显示名（经 P3 主线程化）；失败时退回 "Pawn"。

---

## 5. Prompt 组织与 RAG 合并

为避免将上下文拼入 `user` 文本，RAG 合并应进入 System 段或 ContextBlocks。

接口扩展（建议）：

```csharp
// PromptBuildRequest 新增：由上游注入的额外上下文块
public sealed class PromptBuildRequest {
    ...
    public System.Collections.Generic.IReadOnlyList<ContextBlock> ExternalBlocks { get; set; }
}
```

组装策略：
- PromptService 合并 `ExternalBlocks` 到最终 `ContextBlocks`，并做总预算裁剪。
- 工具结果建议以“简要标题 + 精简 JSON/键值表”呈现；必要时只保留关键字段与统计概览。

---

## 6. ChatUI 改造（SendCommand 流程）

步骤：
1) 取消旧流 → 进入指令会话态（指示灯复位，仅对后续流式有效）。
2) 调用编排：`IOrchestrationService.ExecuteAsync(finalUserText, participantIds, options)`。
3) 若 `result.PlanTrace` 非空：
   - 将 `PlanTrace[0]` 插入 UI 消息流（AI 侧“说明”消息）；
   - 无需额外手写历史记录，编排层已写入（AppendAiNoteAsync）。
4) 构造 Prompt：在 `PromptBuildRequest.ExternalBlocks` 中注入“工具结果 RAG 块”。
5) 发起真流式请求并展示分片；流式期间允许取消与指示灯闪烁。
6) 完成后将“用户最后一条 + LLM 最终文本”写入历史（AppendPairAsync）。

边界：
- 无命中工具：`PlanTrace` 为空，RAG 不注入；可直接流式回答。

---

## 7. 本地化与资源

- `IRimAITool.DisplayName` 支持本地化；短期内可在工具实现中返回固定 zh-Hans 文案。
- 叙述器模板（每语言一条或多条可选模板），通过配置项或资源表热重载（P1 配置热重载广播）。

---

## 8. 日志与纪律

- 日志前缀：`[RimAI.Core][P12]`；关键点记录：mode/profile/exposed/decided/executed/latency/hash(conv)。
- 纪律：
  - 后台非流式：编排与历史写入一律非流式；仅 ChatUI 使用流式。
  - 访问边界：编排不直接触达 Framework/Verse（除通过 P2/P3 的规定入口）。
  - 注入纪律：仅构造函数注入；禁止 Service Locator（后续将移除 ToolRegistry 的 Resolve 退路）。

---

## 9. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Orchestration/
        OrchestrationService.cs          // 扩展：HitDisplayName + PlanTrace 生成与历史写入
        OrchestrationNarrator.cs         // 新增：过程文案本地化生成器
      Tooling/
        IRimAITool.cs                    // 扩展：DisplayName
      History/
        IHistoryService.cs               // 扩展：AppendAiNoteAsync
        HistoryService.cs                // 实现：AI Note（TurnOrdinal=null）
    UI/
      ChatWindow/
        ChatController.cs                // 改造：两段式；RAG 合并 + 真流式
    Modules/
      Prompting/
        Models/PromptModels.cs           // 扩展：PromptBuildRequest.ExternalBlocks
        PromptService.cs                 // 合并 ExternalBlocks 与裁剪
```

---

## 10. 实施步骤（S1→S10）

- S1 工具显示名
  - 为所有演示工具补齐 `DisplayName`；命名示例：
    - `get_colony_status` → “领地资源小助手”
    - `get_pawn_health` → “小人健康状况”
    - `get_beauty_average` → “美观平均”
    - `get_terrain_group_counts` → “地形统计”
    - `get_game_logs` → “游戏日志浏览器”

- S2 编排叙述器
  - 新增 `OrchestrationNarrator.FormatPlanTrace(locale, participantIds, hitDisplayName)`；
  - 返回 zh-Hans 文案；失败返回空字符串。

- S3 编排结果丰富
  - 在 `OrchestrationService.ExecuteAsync` 中：命中后设置 `HitDisplayName`；生成 `PlanTrace`（若为空则不写历史）。
  - 调用 `IHistoryService.AppendAiNoteAsync(convKey, planTraceLine)` 写入历史（不推进回合）。

- S4 历史服务扩展
  - 新增 `AppendAiNoteAsync`：角色=AI、`TurnOrdinal=null`；触发 `OnEntryRecorded` 事件。

- S5 Prompting 扩展
  - `PromptBuildRequest` 新增 `ExternalBlocks`；`PromptService` 合并外部块并做预算裁剪。

- S6 ChatUI 改造
  - `SendCommandAsync`：两段式；将 `PlanTrace[0]` 作为 AI 说明消息插入 UI；RAG 合并后发起真流式，完成时写入最终对。

- S7 日志与可观测
  - 编排成功日志：暴露工具数、决策条数、执行条数、分数摘要、总耗时；
  - ChatUI 打印 outbound Messages（仅 UI 允许）。

- S8 配置与热重载
  - 叙述器模板与工具显示名的本地化后续可接入配置/资源；当前先内置硬编码文本。

- S9 边界自检（grep/Gate）
  - 编排目录不得出现 `StreamResponseAsync\(`；
  - Orchestration 不得直接 `using RimAI.Framework`；
  - 工具/编排不得直接 `System.IO`；
  - 注入纪律：仅构造函数注入。

- S10 回归用例
  - 命令输入“帮我获取殖民地的状态”：
    - 编排命中“领地资源小助手”，PlanTrace 写入历史并显示于 UI；
    - RAG 注入工具结果；
    - LLM 真流式输出自然语言总结；
    - 历史仅新增：1 条用户、1 条最终 AI（含 TurnOrdinal），以及 1 条 AI Note（PlanTrace，TurnOrdinal=null）。

---

## 11. CI / Gate（必须通过）

- 非流式纪律：后台路径 检查=0：`StreamResponseAsync\(`。
- 访问边界：除 `Modules/LLM/**` 外检查=0：`using\s+RimAI\.Framework`；除 `Modules/World/**`、`Modules/Persistence/**` 外检查=0：`using\s+Verse|\bScribe\.`。
- 文件 IO 集中：除 `Modules/Persistence/**` 外检查=0：`System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b|\bStreamReader\b|\bStreamWriter\b`。
- 注入纪律：禁止属性注入与 Service Locator（在演示工具里逐步移除 Resolve 退路）。
- 日志前缀：`[RimAI.Core]` 开头，建议叠加阶段 `[P12]`。

---

## 12. 验收标准（Gate）

1) ChatUI 指令请求两段式闭环可复现（录屏）：
   - 编排命中工具并执行；`PlanTrace` 生成并写入历史（不推进回合）。
   - RAG 注入工具结果；LLM 真流式输出自然语言总结；UI 指示灯在流式期间工作；取消生效。

2) Deep/Wide 未实现：传入返回 `profile_not_implemented`。

3) 历史与摘要：
   - 历史线程包含：用户发言（本轮）、AI Note（PlanTrace，TurnOrdinal=null）、AI 最终输出（本轮 TurnOrdinal=+1）。
   - Recap 仅使用 `TurnOrdinal.HasValue` 的 AI 条目，不包含 AI Note。

4) Gate：所有违规项为 0；日志前缀合规。

---

## 13. 风险与缓解

- 过程文案体量膨胀 → 统一裁剪长度（例如 200 字内）；仅保留首条 PlanTrace。
- 工具结果过大 → RAG 预算裁剪；仅提供摘要/关键字段。
- 本地化缺失 → 回退到 zh-Hans 内置文本；允许配置后续热重载。
- 工具执行失败 → 过程文案可生成“未找到合适工具/执行失败”的玩家可读提示。

---

## 14. 变更记录

- v5-P12：新增“指令模式两段式”流程；`IRimAITool.DisplayName`；编排叙述器；`AppendAiNoteAsync`；`PlanTrace` 必须写入历史；Prompting 支持外部 RAG 块；ChatUI 改造为 RAG→流式最终回答。

---

本文为 V5 P12 的唯一权威实施说明。实现与验收以本文为准。


