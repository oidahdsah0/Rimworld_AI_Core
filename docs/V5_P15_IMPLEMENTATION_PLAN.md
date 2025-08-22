# RimAI V5 — P15 实施计划（安全持久化与更新兼容）

> 目标：确保“安全持久化、更新安全、玩家更新后无需开新档”。在不破坏既有玩法与性能预算的前提下，完成存档 Schema 演进、读档期无感迁移、设置文件原子写入与回退、以及全链可观测。
>
> 全局对齐：遵循《V5 — 全局纪律与统一规范》（`docs/V5_GLOBAL_CONVENTIONS.md`）与《ARCHITECTURE_V5.md》。Gate 用 Cursor 内置工具执行；后台/服务路径一律非流式；所有日志必须以 `[RimAI.Core]` 开头（建议叠加阶段标记）。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 存档 Schema 演进与读档期内存迁移（P6 持久化主导）。
  - 设置文件（索引/模板/预设/本地化等）原子写入 + 备份 + 校验 + 回退。
  - 缺节点/损坏数据的安全默认与后台修复（历史双索引重建等）。
  - 可观测与 Gate：迁移审计、原子写入审计、CI 样本读档验证。
  - 配置项补齐：开启/关闭安全写、备份后缀、写后校验、迁移策略开关。

- 非目标（后续或他阶段处理）
  - LLM 网关（P2）与编排（P5）业务逻辑变更。
  - UI 行为变更（ChatWindow 仍仅 UI 真流式，后台非流式）。
  - 非持久化的性能重构（保持既有预算与行为）。

---

## 1. 设计原则（Invariants）

- 合同最小面：继续仅对外暴露 `IConfigurationService` + `CoreConfigSnapshot`。
- 访问边界：Framework 仅 P2；Verse 仅 P3/P6；文件 IO 仅经 P6 的 `IPersistenceService`。
- 非流式纪律：后台/服务路径一律非流式；仅 ChatWindow UI 允许真流式。
- 日志与可观测：所有日志前缀必须以 `[RimAI.Core]` 开头，建议 `[P6]`/`[P4]`/`[P13]` 等叠加。
- .NET 4.7.2：所有实现需兼容 .NET Framework 4.7.2。

---

## 2. 方案总览

### 2.1 Schema 版本化与读档迁移
- 节点命名保留后缀 `RimAI_*Vn`，节点内保留 `schemaVersion` 字段（向后兼容）。
- 读档时：`LoadAll()` 按节点 `schemaVersion` 执行按序 `IMigrationStep`（幂等、可重入、可跳过），产出最新内存快照。
- 写档时：始终写入“当前版本”的节点，不额外写盘动作，玩家无感升级。
- 可选：新增根 `RimAI_SchemaVersionsV1`（字典：节点名→版本）以便跨节点快速判定迁移链；缺失不阻塞读档。

### 2.2 设置文件原子写入与回退
- 写入流程：`*.tmp` 写入 → Flush（尽可能 fsync）→ 原子替换目标文件 → 同步/更新 `*.bak` 备份。
- 写后校验：反序列化回读 + 必要字段检查；失败则回退到 `.bak` 并打审计日志。
- 读路径回退：主文件失败 → 读 `.bak`；均失败 → 空状态 + 后台重建（索引/模板）。

### 2.3 缺节点与索引重建
- 历史双索引（ConvKey/Participant）缺失或不一致：后台重建（现有实现基础上强化日志与计数）。
- 其他节点缺失：安全默认（空集合/空结构）；必要时排队一次性修复任务。

### 2.4 可观测性
- `PersistenceStats` 扩展：迁移步数/耗时、原子写入状态、回退来源、校验失败计数、节点读写耗时分布。
- 统一日志：`[RimAI.Core][P6.Persistence] ...`；禁止输出敏感正文，打印键/计数/指纹/耗时。

---

## 3. 目录与触点（现状复核）

- 存档与持久化（强影响）
  - `RimAI.Core/Source/Modules/Persistence/PersistenceService.cs`
  - `RimAI.Core/Source/Modules/Persistence/PersistenceManager.cs`
  - `RimAI.Core/Source/Modules/Persistence/Snapshots/**`
  - `RimAI.Core/Source/Modules/Persistence/ScribeAdapters/**`
- 历史/提要（中强影响）
  - `RimAI.Core/Source/Modules/History/**`、`.../Recap/**`
- 工具索引/设置文件（中影响）
  - `RimAI.Core/Source/Modules/Tooling/Indexing/ToolIndexStorage.cs`
  - `RimAI.Core/Source/Infrastructure/Localization/LocalizationService.cs`
  - `RimAI.Core/Source/Modules/Persona/Templates/PersonaTemplateManager.cs`
  - `RimAI.Core/Source/Modules/Server/ServerPromptPresetManager.cs`
- 配置（轻中影响）
  - `RimAI.Core/Source/Infrastructure/Configuration/CoreConfig.cs`
- Debug 面板（轻影响）
  - `docs/V5Debug/DebugPanel/Parts/P6_PersistencePanel.cs`

---

## 4. 实施任务清单（逐项可验收）

1) 读档迁移执行器
- 定义 `IMigrationStep { string Node; int From; int To; Task<bool> ApplyAsync(PersistenceSnapshot snap, CancellationToken ct) }`。
- `PersistenceService.LoadAll()` 内按节点 `schemaVersion` 收集可用步骤并依序执行；失败记录并不中断整体加载（局部空状态）。
- 现阶段示例（按需补充，不强制落库）：
  - Conversations：v1→v2（convId 归并到 convKey、缺 `ParticipantIds` 的补齐）。
  - Recap：校正 `Mode`、截断过长文本（已按 `_maxTextLength`）。
  - Servers：兼容旧字段别名（`JsonProperty`），补齐 `InspectionSlots.NextDueAbsTicks`。

2) 原子写入工具化
- 在 `IPersistenceService` 增加/明确安全写契约（可沿用 `WriteTextUnderConfigAsync` 的签名语义升级）。
- 内部实现：`*.tmp` + Flush(+fsync) → 原子替换 → `.bak`；失败回退；写后反序列化校验钩子（委托/策略）。
- 将以下读写切换到安全路径：
  - `ToolIndexStorage.SaveAsync/LoadOrNullAsync`。
  - `LocalizationService` 的本地化 JSON 读路径增加 `.bak` 回退。
  - `PersonaTemplateManager`/`ServerPromptPresetManager` 的 JSON 读路径增加 `.bak` 回退。

3) 缺节点/损坏容错
- `LoadAll()`：所有节点均使用“TryRead + 安全默认”策略（空集合/空结构）。
- 历史双索引：强化 `RebuildHistoryIndexesIfNeeded` 的差异检测与计数日志。
- 失败不致命：将迁移/校验失败记录到 `PersistenceStats`，并在 Debug 面板提供“一键修复/重建”。

4) 配置项与默认值
- `CoreConfig.Persistence` 新增：
  - `EnableAtomicWrite = true`。
  - `EnableBackup = true`。
  - `BackupSuffix = ".bak"`。
  - `ValidateOnWrite = true`。
  - `Migrations.Enabled = true`。
- 默认值保持向后兼容；禁用时退化为当前实现但打印警告。

5) 可观测与 Debug
- 扩展 `PersistenceStats` 字段：`MigrationsApplied`、`AtomicWrites`、`FallbackFromBak`、`ValidationErrors`。
- Debug 面板（P6）：
  - “验证当前快照可写/可读”按钮（写临时文件→再读→比对关键节点计数）。
  - “重建历史索引”按钮（现有能力增强为显式按钮）。
  - “导出/导入快照（JSON）”保留，但走安全写与校验路径。

6) Gate/CI 与样本
- 样本存档：`samples/saves/{v_min, v_prev, v_curr}.rws`（仓内元数据占位，CI 外部路径挂载）。
- CI 步骤：
  - 逐个加载样本存档→断言无异常→断言关键节点存在→断言 `PersistenceStats` 迁移/重建计数可接受。
  - 静态 Gate：
    - 除 `Modules/World/**`、`Modules/Persistence/**` 外不允许 `\bScribe\.`。
    - 除 `Modules/Persistence/**` 外不允许 `using System.IO|File\.|Directory\.|FileStream|StreamReader|StreamWriter`。
    - 后台非流式：除 `Source/UI/ChatWindow/**` 外 `StreamResponseAsync\(` = 0。

---

## 5. 默认配置（建议）

```json
{
  "Persistence": {
    "MaxTextLength": 4000,
    "EnableDebugExport": true,
    "NodeTimeoutMs": 200,
    "OnLoadRebuildIndexes": true,
    "EnableAtomicWrite": true,
    "EnableBackup": true,
    "BackupSuffix": ".bak",
    "ValidateOnWrite": true,
    "Migrations": { "Enabled": true },
    "Files": {
      "BasePath": "Config/RimAI",
      "IndicesPath": "Config/RimAI/Indices"
    }
  }
}
```

---

## 6. 验收标准（Gate + 手验）

- 读档无感：加载上个稳定版本存档不报错，可立即进入游戏。
- 一致性：首次正常存档后，节点统一写新版本（ConversationsV2 等）；再次读档一致。
- 原子写：拔电测试/模拟写入失败后，存在 `.bak` 回退，索引/模板可恢复，UI 不崩溃。
- 历史索引：缺失/损坏样本可被自动/手动重建；计数与关系一致。
- Gate：
  - Verse/Scribe 面最小化规则通过。
  - 文件 IO 集中规则通过。
  - 后台非流式规则通过。
  - 日志抽样前缀检查通过（`[RimAI.Core]`）。
- 性能：读档 ≤ 200ms（中档样本）；保存 ≤ 150ms；迁移总耗时可观测并在 100ms 级别内（中档样本）。

---

## 7. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 旧档字段异常/枚举扩展 | 多路径 TryRead + Unknown 兜底 + 审计日志 |
| 写入中断导致文件损坏 | 原子写 + `.bak` 备份 + 写后校验 + 回退 |
| 迁移长耗时 | 分节点/分步执行，必要时后台修复，统计耗时 |
| Gate 误报 | 区分 Path/字符串操作与真正 IO；仅禁止直接文件 IO |
| 玩家手动修改文件 | 读侧严格校验与回退，避免崩溃 |

---

## 8. 提交与回归

- PR 内容：
  - `PersistenceService` 与 `IPersistenceService` 安全写实现/契约更新。
  - `ToolIndexStorage/LocalizationService/PersonaTemplateManager/ServerPromptPresetManager` 适配 `.bak` 回退。
  - `CoreConfig` 新配置字段与默认值。
  - `DebugPanel` 新按钮与统计展示。
  - `ARCHITECTURE_V5.md`/`V5_GLOBAL_CONVENTIONS.md` 更新“安全持久化”与 Gate（如有）。
- 回归：
  - 样本存档读档 + 写档 + 再读验证。
  - 人工断电/删除主文件仅留 `.bak` 验证回退。
  - 历史索引重建正确性（随机抽样 convKey/participants）。

---

## 9. 附：接口与数据结构草案（示意）

- 迁移接口
```csharp
internal interface IMigrationStep
{
    string Node { get; }
    int From { get; }
    int To { get; }
    System.Threading.Tasks.Task<bool> ApplyAsync(PersistenceSnapshot snap, System.Threading.CancellationToken ct);
}
```

- 安全写策略（内部）
```csharp
internal sealed class AtomicWriteOptions
{
    public bool EnableBackup { get; set; } = true;
    public string BackupSuffix { get; set; } = ".bak";
    public bool ValidateOnWrite { get; set; } = true;
}
```

- 统计扩展（示意）
```csharp
public sealed class PersistenceStats
{
    public string Operation { get; set; }
    public long ElapsedMs { get; set; }
    public int Nodes { get; set; }
    public int MigrationsApplied { get; set; }
    public int AtomicWrites { get; set; }
    public int FallbackFromBak { get; set; }
    public int ValidationErrors { get; set; }
}
```

---

## 10. 时间与里程碑（建议）

- W1：方案评审 + 配置/接口冻结 + CI 样本准备。
- W2：原子写与回退落地 + ToolIndex/模板/本地化适配。
- W3：读档迁移器 + 历史索引重建强化 + Debug 面板。
- W4：性能与故障演练 + Gate/CI 绿灯 + 文档回填。

> 本文自合入起生效。提交涉及跨阶段边界的改动，请在 PR 中引用本文件并更新相关章节。
