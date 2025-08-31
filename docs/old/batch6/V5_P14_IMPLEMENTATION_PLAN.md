# RimAI V5 — P14 实施计划（历史记录JSON化与群聊写入翻新）

> 最新注意事项（Debug 页面移除 & 非流式纪律）
>
> - Debug 页面（DebugPanel/Debug Window）已从项目中移除；流式仅允许在 ChatWindow UI 路径展示；后台/服务路径一律非流式。
> - Gate 更新：关于流式 API 的允许路径应视为仅 `Source/UI/ChatWindow/**`；`Source/UI/DebugPanel/**` 不再适用。
> - 所有文件读写一律通过 `IPersistenceService` 提供的统一文件 IO 接口完成；除 `Modules/Persistence/**` 外禁止直接使用 `System.IO` 与 Scribe。
> - 日志必须以 `[RimAI.Core]` 开头，建议叠加阶段标识，如 `[RimAI.Core][P14]`。

> 版本：v5.0.0-alpha（P14）

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 历史记录统一切换为“JSON 留存”的新格式（5 字段）：入口/发言人/时间/类型/内容。
  - 重构历史写入接口为唯一入口（AppendRecord），删除旧有成对写入与角色化写入接口。
  - 群聊（小人群聊、服务器群聊）强制 LLM 输出“结构化 JSON（数组对象）”，解析后“每句单独入档”。
  - 历史查询接口对 ChatUI 与 Prompt 作曲器保持无感（展示与组装不受影响）。
  - 新增世界时间获取接口（游戏内年月象限制式），历史记录统一使用该时间串。

- 非目标（后续阶段可能扩展）
  - 新 UI/可视化调整（本阶段保持 ChatUI 行为与样式不变）。
  - 扩展更多历史类型或并行会话写入策略（仅奠定 JSON 基线）。

---

## 1. 全局不变式与约束对齐

- 非流式纪律：后台/服务型路径一律非流式；仅 `Source/UI/ChatWindow/**` 允许真流式。
- 访问边界：LLM 仅 P2；Verse 仅 P3/P6；文件 IO 仅 P6；Tool JSON 唯一产出 P4；提示词单入口 P11。
- 对外最小合同不变：Contracts 仍仅 `IConfigurationService` 与 `CoreConfigSnapshot`。
- 日志：所有日志以 `[RimAI.Core]` 开头，建议附 `[P14]`。
- 本阶段“不兼容旧版本”，删除旧写入方法，代码必须保持干净、健壮。

---

## 2. 新历史JSON格式（唯一权威）

- 记录结构（字段名固定，均为字符串或可序列化文本）：
  - 入口 entry："ChatUI" | "Stage:<ActName>"
  - 发言人 speaker：全局唯一 ID（`player:<saveId>` | `pawn:<loadId>` | `thing:<loadId>` | `tool:<name>` | `agent:stage`）
  - 时间 time：游戏内时间字符串（`GenDate.DateFullStringAt(abs, longLat)` 格式）
  - 类型 type："chat" | "tool_call"
  - 内容 content：最终展示文本（或工具结果的裁剪 JSON 文本）

- 存档与内存均以“上述 JSON 串”作为 `HistoryEntry.Content` 的唯一内容。
- 读取端（供 UI/Prompt 使用）默认解包出 `content` 作为展示文本；`Role` 由 `speaker` 推导（`player:*`→User，其余→Ai）。

示例：
```json
{
  "entry": "Stage:InterServerGroupChat",
  "speaker": "thing:123",
  "time": "5502年 4象限 9日 12:03",
  "type": "chat",
  "content": "负载稳定，建议下一轮巡检关注风冷效率。"
}
```

---

## 3. 世界时间接口（P3 扩展）

- 新增：`IWorldDataService.GetCurrentGameTimeStringAsync(CancellationToken ct = default)`
  - 实现：主线程化读取 `Find.TickManager.TicksAbs` 与 `Find.WorldGrid.LongLatOf(tile)`，返回 `GenDate.DateFullStringAt(abs, longLat)`。
  - 作为历史时间 `time` 的统一来源；失败回退为 `DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")`。

---

## 4. 历史服务重构（P8 History）

- 新接口（唯一写入入口）：
  - `Task AppendRecordAsync(string convKey, string entry, string speaker, string type, string content, bool advanceTurn, CancellationToken ct = default)`
  - 职责：
    - 统一从 `IWorldDataService` 获取 `time`；
    - 组装 5 字段 JSON 并写入；
    - `advanceTurn=true` 时推进单调回合序号（仅用于 AI 最终输出等需要推进回合的场景）。

- 删除（不保留兼容）：
  - `AppendPairAsync`、`AppendUserAsync`、`AppendAiFinalAsync`、`AppendAiNoteAsync`。

- 读取保持接口不变：
  - `GetThreadAsync/GetAllEntriesAsync` 返回的 `HistoryEntry.Content` 自动解包为 `payload.content`（纯文本）供 UI/Prompt 使用；
  - 若需要原始 JSON，后续可增只读 `GetThreadRawAsync`（非本阶段目标）。

- 编辑/删除：
  - `EditEntryAsync` 仅覆盖 JSON 中的 `content` 字段；删除/撤销逻辑不变。

---

## 5. 群聊输出与写入规范（P9 Acts + P11 Prompting）

- LLM 输出格式（两类群聊统一）：
  - 强制仅输出 JSON 数组，每个元素：`{ "speaker": "pawn:<id>|thing:<id>", "content": "..." }`；
  - `speaker` 必须在参与者白名单内；禁止额外解释文本或非 JSON 内容；
  - SystemPrompt 与 User 指令处明确上述格式与白名单；继续非流式调用。

- 小人群聊 `GroupChatAct`：
  - 解析数组后，将每个元素写为单独历史记录：
    - `AppendRecordAsync(convKey, "Stage:GroupChat", speaker, "chat", content, advanceTurn=false)`；
  - Act 返回文本可继续拼接“第N轮/显示名”供 `StageHistorySink` 打印摘要，但每句已作为独立节点入档。

- 服务器群聊 `InterServerGroupChatAct`：
  - 同上，`speaker=thing:<id>`；
  - 会话使用真实 convKey，与玩家↔服务器对话共享历史。

- 提示词（P11）：
  - 在群聊专用 Scope 的作曲器中，增加“参与者白名单 + JSON 合约”约束行；
  - 其余上下文（环境/参与者摘要/服务器人格与温度等）保持现有作曲器或新增作曲器提供，不触达历史结构。

---

## 6. ChatUI 与 Orchestration 写入

- ChatUI（玩家 ↔ 小人）：
  - 玩家消息：`AppendRecordAsync(convKey, "ChatUI", "player:<id>", "chat", userText, advanceTurn=false)`；
  - AI 最终：`AppendRecordAsync(convKey, "ChatUI", "pawn:<loadId>", "chat", finalText, advanceTurn=true)`。

- Orchestration（工具调用轨迹，可选纳入）：
  - 工具调用记录：`AppendRecordAsync(convKey, "ChatUI", "tool:<name>", "tool_call", <结果裁剪JSON>, advanceTurn=false)`；
  - PlanTrace/说明性文本：`type="chat"`。

- StageHistorySink：
  - 统一改为 JSON 写入：`AppendRecordAsync(convKeyOrStageConv, "Stage:<ActName>", "agent:stage", "chat", finalText, advanceTurn=false)`。

---

## 7. 数据模型与持久化

- `PersistenceSnapshot.ConversationEntry.Text` 直接存储 5 字段 JSON 字符串。
- 读档还原时无需迁移逻辑（不兼容旧式文本/Role），按 JSON 直接恢复。

---

## 8. 目录结构与文件（受影响清单）

- P3：`Source/Modules/World/IWorldDataService.cs`、`WorldDataService.cs`（新增 `GetCurrentGameTimeStringAsync`）。
- P8：`Source/Modules/History/IHistoryService.cs`、`HistoryService.cs`（新增 `AppendRecordAsync`，删除旧写入方法）。
- P9：`Source/Modules/Stage/History/StageHistorySink.cs`（JSON 写入）、`Acts/GroupChatAct.cs`、`Acts/InterServerGroupChatAct.cs`（解析新 JSON 与逐条写入）。
- P11：群聊 Scope 作曲器（新增/微调，加入 JSON 合约约束行）。
- P6：持久化读取/写入沿用，无需结构变更（文本即 JSON）。

---

## 9. CI/Gate（必须通过）

- 后台非流式：检查 `StreamResponseAsync\(` → 0（仅 `Source/UI/ChatWindow/**` 允许）。
- Verse 最小面：除 `Modules/World/**`、`Modules/Persistence/**` 外，检查 `\bScribe\.|using\s+Verse` → 0。
- 文件 IO 集中：除 `Modules/Persistence/**` 外，检查 `using\s+System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b|\bStreamReader\b|\bStreamWriter\b` → 0。
- 历史写入统一：检查旧接口调用 `AppendPairAsync|AppendUserAsync|AppendAiFinalAsync|AppendAiNoteAsync` → 0；仅允许 `AppendRecordAsync`。
- 日志前缀审计：`Log\.(Message|Warning|Error)\(` 文本以 `[RimAI.Core]` 开头。

---

## 10. 验收 Gate（必须全绿）

- JSON 留存：
  - 新写入路径均产出含 5 字段的 JSON；随机抽样 10 条验证字段完整且符合值域（entry/speaker/time/type/content）。
  - 时间字段为游戏内时间串（含年份/象限/日期/时间），与 `WorldApiV16` 输出一致。

- ChatUI/Prompt 无感：
  - ChatUI 历史渲染与用户体验不变；
  - P11 作曲器（含 HistoryRecapComposer）在获取历史时不需改动即可正常工作（读取 `content` 纯文本）。

- 群聊：
  - 小人群聊与服务器群聊的 LLM 输出均为数组对象；
  - 同一轮内，对象数组解析后“每句单独作为历史记录”写入；
  - 服务器群聊与玩家↔服务器对话复用同一 convKey。

- 旧接口移除：
  - 代码库中不存在旧写入接口实现与调用；构建与 Gate 均通过。

---

## 11. 实施步骤（建议顺序）

1) P3：实现 `GetCurrentGameTimeStringAsync`；单元验证返回格式与 `WorldApiV16` 一致。
2) P8：新增 `AppendRecordAsync` 并切换内部存储为 5 字段 JSON；删除旧写入接口；读取时解包 `content` 与 `speaker→Role` 推导。
3) 替换调用方：
   - ChatUI `ChatController` 两次写入改为两次 `AppendRecordAsync`；
   - `StageHistorySink` 改为 JSON 写入；
   - Orchestration（若留存工具调用轨迹）改为 `tool_call` 类型；
   - `GroupChatAct`/`InterServerGroupChatAct` 改造解析新数组 JSON，并逐条写入历史。
4) P11：群聊 Scope 作曲器增加“参与者白名单 + JSON 合约”约束行（System 段），确保 LLM 严格输出数组对象。
5) 持久化回归：存档/读档/导入/导出验证历史节点与 ChatUI 展示一致。
6) Gate/验收：跑通 §9/§10 列表；整理录屏与说明。

---

## 12. 风险与缓解

| 风险 | 缓解策略 |
|------|----------|
| 第三方模型越权输出非 JSON | System/User 双重强调；解析端强校验并失败容错（整轮丢弃或仅采纳合法项），同时记录告警日志 |
| 历史条目膨胀（群聊每句入档） | 可按配置裁剪极长文本；Recap 仍仅总结“最终输出”；分页渲染避免一次加载过多 |
| 时间获取失败 | 回退 UTC 文本时间；打印告警，后续重试 |
| 旧接口遗漏 | Gate 检查旧方法名；CR 与自检清单双重把关 |

---

## 13. 变更记录

- v5-P14：历史记录统一 JSON 化（入口/发言人/时间/类型/内容）；群聊 LLM 输出数组对象，每句独立入档；新增世界时间接口；重构写入入口为 `AppendRecordAsync` 并删除旧接口；ChatUI/Prompt 行为无感。

---

> 本文为 V5 P14 唯一权威实施说明。实现与验收以本文为准；若与《V5 — 全局纪律与统一规范》冲突，以全局规范为准。
