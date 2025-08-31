# RimAI V5 — P6 实施计划（Persistence：发令员-总工程师-专家）

> 目标：一次性交付“稳定、可观测、可恢复”的持久化子系统，采用“发令员-总工程师-专家”职责分离模型，实现 Core 长期状态的安全落盘与读档恢复。本文档为唯一入口，无需查阅旧文即可完成落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 架构落地：`PersistenceManager(GameComponent)`（发令员）→ `IPersistenceService`（总工程师，唯一 Scribe 层）→ 各领域服务快照（专家）。
  - 节点与快照：历史 v2 主存与双索引、前情提要、固定提示词、人物传记、人格绑定、个人观点与意识形态、舞台素材（预留）。
  - 可观测：Debug 面板“Persistence”分节（节点统计/导出导入/索引重建自检）、统一日志前缀、耗时与条目统计。
  - 纪律与 CI：Scribe/Verse 使用面最小化（仅限 Persistence 模块文件），节点级容错与版本化、读取缺失节点默认空状态。

- 非目标（后续阶段处理）
  - 不引入新历史查询/策略/RAG；Embedding/向量索引/缓存/合流一律不入档（可重建）。工具索引仅以设置文件 JSON 持久化。
  - 不做旧档 v1 的迁移读取；本阶段以 v2 新档为准（与 V4/P10 约束一致）。

---

## 1. 架构总览（全局视角）

- 发令员（入口）
  - `PersistenceManager : GameComponent` 拦截 RimWorld 存读档生命周期（`ExposeData()`），根据 `Scribe.mode` 调用 `IPersistenceService.SaveAll/LoadAll`。

- 总工程师（唯一 Scribe 执行者）
  - `IPersistenceService` 是 Core 内部唯一允许 `using Verse.Scribe` 的位置；负责各节点的读写、版本化、错误隔离、统计与导入导出。
  - 暴露领域级强类型方法（例如 `PersistHistory(HistoryState)` / `LoadHistory()`），以及组合入口 `SaveAll/LoadAll`。

- 专家（领域服务）
  - 各业务服务（History/Recap/FixedPrompts/Biography/PersonaBindings/Beliefs/StageRecap）仅提供“不可变快照”导出/导入方法；不出现任何 Scribe/Verse 依赖。
  - 服务内部保障线程安全（读写锁/原子交换），以便在保存时快速抓取一致快照、在读档时原子写回状态。

---

## 2. 节点与数据模型（版本化命名）

> 所有节点命名包含版本后缀；每节点内部再写入 `schemaVersion:int` 以备未来软迁移。

- 历史账本 v2（仅最终输出）
  - `RimAI_ConversationsV2`：`conversationId → ConversationRecord { ParticipantIds: string[], Entries: ConversationEntry[] }`
  - `RimAI_ConvKeyIndexV2`：`convKey → List<conversationId>`
  - `RimAI_ParticipantIndexV2`：`participantId → List<conversationId>`

- 前情提要（Recap）
  - `RimAI_RecapV1`：`conversationId → List<RecapSnapshotItem { id, text, createdAt }]`

- 个性化域（与历史解耦）
  - `RimAI_FixedPromptsV1`：`pawnId → text`
  - `RimAI_BiographiesV1`：`pawnId → List<BiographyItem { id, text, createdAt, source }]`
  - `RimAI_PersonaBindingsV1`：`pawnId → personaName#rev`
  - `RimAI_PersonalBeliefsV1`：`pawnId → PersonalBeliefs { worldview, values, codeOfConduct, traitsText }`

- 舞台素材（P11.5 预留）
  - `RimAI_StageRecapV1`：`List<ActRecapEntry { title, triggerAtUtc, actName, convKey, participants[], summaryText, metadataJson?, tags[], createdAtUtc }]`

- 严格不入档
  - Embedding/RAG 向量、任何 provider/model 缓存或合流产物、临时队列或运行时状态、工具向量索引（仅设置文件 JSON）。

---

## 3. 接口契约（内部 API，仅示意签名）

> 下述接口位于 Core 内部命名空间，不进入 `RimAI.Core.Contracts` 稳定层；所有 DTO/快照为 POCO/基础类型集合，严禁 Verse 类型。

```csharp
// Modules/Persistence/IPersistenceService.cs
internal interface IPersistenceService
{
    // 组合入口
    void SaveAll(PersistenceSnapshot snapshot);
    PersistenceSnapshot LoadAll();

    // 分域接口（示例）
    void PersistHistory(HistoryState state);
    HistoryState LoadHistory();

    void PersistRecap(RecapState state);
    RecapState LoadRecap();

    void PersistFixedPrompts(FixedPromptsSnapshot state);
    FixedPromptsSnapshot LoadFixedPrompts();

    // ... 其余域同理（Biography/PersonaBindings/PersonalBeliefs/StageRecap）

    PersistenceStats GetLastStats();

    // Debug 辅助
    string ExportAllToJson();
    void ImportAllFromJson(string json);
}

// Game 入口：Modules/Persistence/PersistenceManager.cs
internal sealed class PersistenceManager : GameComponent
{
    public override void ExposeData()
    {
        var svc = CoreServices.Persistence; // 仅此处允许定位（或通过注入）
        if (Scribe.mode == LoadSaveMode.Saving)
            svc.SaveAll(BuildSnapshotFromServices());
        else if (Scribe.mode == LoadSaveMode.LoadingVars)
            ApplySnapshotToServices(svc.LoadAll());
    }
}
```

> 各领域服务需提供：`ExportSnapshot()` / `ImportSnapshot(state)`，内部完成状态的线程安全读/写。

---

## 4. 目录结构与文件（建议）

```
RimAI.Core/
  Source/
    Modules/
      Persistence/
        IPersistenceService.cs
        PersistenceService.cs            // 唯一 Scribe 层
        PersistenceManager.cs            // GameComponent 入口
        Snapshots/
          HistoryState.cs
          RecapState.cs
          FixedPromptsSnapshot.cs
          BiographySnapshot.cs
          PersonaBindingsSnapshot.cs
          PersonalBeliefsState.cs
          StageRecapState.cs
      // ToolingIndexState.cs            // 已移除：工具索引不入档，只做设置文件 JSON
        ScribeAdapters/
          Scribe_Dict.cs                 // 字典/列表/POCO 读写辅助
          Scribe_Poco.cs
        Diagnostics/
          PersistenceStats.cs
    UI/DebugPanel/Parts/
      P6_PersistencePanel.cs            // 节点统计/导出导入/索引自检
```

---

## 5. 配置（内部 CoreConfig.Persistence，非对外 Snapshot）

> 通过 `IConfigurationService` 读取；仅内部消费，不新增 `CoreConfigSnapshot` 字段。

```json
{
  "Persistence": {
    "MaxTextLength": 4000,
    "EnableDebugExport": true,
    "NodeTimeoutMs": 200,
    "OnLoadRebuildIndexes": true,
    "Files": { "BasePath": "Config/RimAI", "IndicesPath": "Config/RimAI/Indices" }
  }
}
```

---

## 6. 实施步骤（一步到位）

> 按 S1→S12 完成；每步可通过 Debug 面板或日志进行自检；无需查阅其他文档。

### S1：定义快照与节点 DTO（Snapshots/*）
- 新建 `HistoryState/RecapState/FixedPromptsSnapshot/BiographySnapshot/PersonaBindingsSnapshot/PersonalBeliefsState/StageRecapState`。
- 要求：仅使用基础类型与 POCO，字段 `init;` 不可变；必要时提供工厂/拷贝构造以简化导入。

### S2：编写 Scribe 适配器（ScribeAdapters/*）
- 提供统一的字典/列表读写助手，封装 `Scribe_Collections.Look`/`Scribe_Values.Look` 常见模式。
- 约定：所有 `Look` 调用都集中在这些适配器与 `PersistenceService.cs` 内，便于搜索与审计。

```csharp
// 示例：字典<string, List<string>> 的读写
public static class Scribe_Dict
{
    public static void Look(ref Dictionary<string, List<string>> dict, string label)
    {
        Scribe_Collections.Look(ref dict, label, LookMode.Value, LookMode.Value);
        dict ??= new Dictionary<string, List<string>>();
    }
}
```

### S3：实现 `IPersistenceService` 接口骨架
- `SaveAll(PersistenceSnapshot)`：顺序写入各节点（见“装载顺序”），节点级 try/catch，记录耗时与条目；失败不阻塞后续节点。
- `LoadAll()`：顺序读取；缺失节点返回默认空状态；必要时重建历史双索引。
- 提供 `ExportAllToJson/ImportAllFromJson` 以支持 Debug 导出/导入（限开发者）。

### S4：Game 入口 `PersistenceManager` 与 DI 接线
- 新建 `PersistenceManager : GameComponent`，在 `ExposeData()` 中分支调用保存/读取。
- 在 `ServiceContainer.Init()` 注册：`IPersistenceService -> PersistenceService`，并 `Resolve<IPersistenceService>()` 自检。

### S5：业务服务快照导出/导入 API
- 为各服务新增：`ExportSnapshot()` / `ImportSnapshot(state)`；内部以原子交换或写锁实现。
- 历史服务需在 `ImportSnapshot` 后重建倒排索引（若 `OnLoadRebuildIndexes=true`）。

### S6：历史 v2 主存与双索引写入
- 写入 `RimAI_ConversationsV2`/`RimAI_ConvKeyIndexV2`/`RimAI_ParticipantIndexV2`，并写入 `schemaVersion`。
- 读档：若主存加载成功但索引缺失/损坏，自动线性重建索引并打印差异统计。

### S7：前情提要/固定提示/传记/绑定/个人观点/舞台素材写入
- 按节点分别写入；对文本字段应用长度上限（来自配置）。
- 读档：不存在节点即返回空；单条过长文本裁剪并在日志中标注 `truncated=true`。

### S8：统一文件 IO（新增职责）
- Persistence 作为全局唯一持久化 IO 入口，提供文件读写 API：`ReadJsonAsync<T>(path)` / `WriteJsonAsync(path, obj)`；统一路径管理（基础路径/索引路径等来自 `CoreConfig.Persistence.Files`）。
- 工具向量索引（P4）仅以设置文件 JSON 形式持久化：Tooling 通过 `IPersistenceService` 的文件 API 读写 `tools_index_{provider}_{model}.json`；不提供任何索引入档/读档接口。

### S9：节点级容错与统计
- 每节点 try/catch：记录 `node`, `ok`, `entries`, `bytesApprox`, `elapsedMs`, `error?`。
- `GetLastStats()` 返回最近一次保存/读取的完整统计，供 Debug 面板展示。

### S10：Debug 面板“Persistence”分节
- 展示 `GetLastStats()` 概览与节点表格。
- 按钮：`Export JSON`（保存到 `Config/RimAI/Snapshots/{saveName}.json`）、`Import JSON`、`Rebuild History Indexes`（运行索引重建并打印差异）。
- 读档后自动在首次打开面板时提示：各节点条目数与耗时。
  - 工具索引页签：显示索引文件路径、存在性、记录数、指纹；按钮支持“重建索引/打开目录”。

### S11：CI/Gate 与纪律（使用 Cursor 内置工具）
- 全仓检查（必须为 0，除 `Modules/Persistence` 下）：`\bScribe\.` 与 `using\s+Verse`。
- `Modules/Persistence` 内仅允许在 `PersistenceService.cs`、`PersistenceManager.cs`、`ScribeAdapters/*` 出现上述引用。

### S12：性能与预算自检
- 中等规模存档：保存 ≤ 150ms、读档 ≤ 200ms；超过打印 Warn 并在 Debug 显示。
- 文本节点（传记/Recap）按配置上限裁剪；统计被裁剪的条目数。

### S13：文档与录屏
- 更新 Debug 面板使用说明，附短录屏：保存→导出→清空→导入→读档恢复的完整流程。

---

## 7. 读档顺序、校验与容错细节

- 顺序建议
  1) 历史 v2 主存（Conversations）
  2) 历史索引（ConvKey/Participant）（缺失可重建）
  3) 前情提要（依赖 conversationId）
  4) 固定提示/传记/人格绑定/个人观点
  5) 工具向量索引（不入档，跳过）
  6) 舞台素材（可选）

- ID 与数据校验
  - 参与者 ID 统一格式：`pawn:<loadId>` / `player:<saveInstanceId>` / `persona:<name>#<rev>`；非法项丢弃并记录。
  - `convKey = join('|', sort(participantIds))` 一致性校验；不一致时以现算为准并更新索引。

- 缺失与损坏策略
  - 缺失节点：返回空状态；相关功能降级（例如历史列表为空）。
  - 节点读取异常：记录错误并继续；最终返回的 `PersistenceSnapshot` 中该域为空。

---

## 8. Scribe 模式与示例（集中于 Persistence 层）

```csharp
// 写入 History 主存（示例）
private void WriteConversationsNode(Dictionary<string, ConversationRecord> conversations)
{
    int schemaVersion = 2;
    Scribe_Values.Look(ref schemaVersion, "schemaVersion", 2);
    // 键=conversationId，值=POCO ConversationRecord
    Scribe_Collections.Look(ref conversations, "items", LookMode.Value, LookMode.Deep);
}

// 读取 History 索引（示例）
private void ReadConvKeyIndexNode(ref Dictionary<string, List<string>> index)
{
    Scribe_Collections.Look(ref index, "items", LookMode.Value, LookMode.Value);
    index ??= new();
}
```

> 提示：RimWorld 版本差异可能导致 `LookMode` 行为变化，需以本 Mod 的最低目标版本实际验证；若序列化 POCO 复杂，可拆为并行列表结构存储后在加载期重建。

---

## 9. 日志与可观测（统一前缀）

- 前缀：`[RimAI.P6.Persistence]`。
- 保存/读取汇总：`op=save|load, nodes=7, elapsed=123ms`。
- 节点级：`node=RimAI_ConversationsV2, ok=true, entries=24, bytes≈18.2KB, elapsed=31ms`。
- 索引重建：`rebuild=HistoryIndexes, convKeyFixed=3, participantFixed=5, elapsed=9ms`。
  - 工具索引：`toolIndex=file=tools_index_{provider}_{model}.json, fingerprint=..., entries=102, elapsed=22ms`。

---

## 10. CI/Gate（使用 Cursor 内置工具，必须通过）

- Scribe/Verse 面最小化
- 全仓 检查=0（排除 `Modules/Persistence/**`）：`\bScribe\.|using\s+Verse`。
  - 仅 `PersistenceService.cs`、`PersistenceManager.cs`、`ScribeAdapters/*` 允许出现上述引用。

- 注入纪律
  - 仅构造函数注入；禁止属性注入与随处 `CoreServices`。

- 快照纯净
  - Snapshots/* 不得有 Verse 依赖；DTO 仅基础类型与 POCO。

---

## 11. Gate（验收标准）

- 基础能力
  - 新建存档：写入全部节点；读档后各域状态一致（抽样字段 Hash 稳定）。
  - 缺失任一节点：系统可继续运行，相关域降级；Debug 面板给出可读提示。

- 可观测
  - Debug 面板“Persistence”展示节点统计、导出/导入按钮、索引重建按钮与结果摘要。
  - 日志含保存/读取总耗时与节点级统计。

- 纪律
- CI/Gate（Cursor 内置工具）通过（Scribe/Verse 面最小化；Snapshots 纯净）。

- 性能
  - 中等规模存档保存 ≤ 150ms；读档 ≤ 200ms；超标打印 Warn。
  - 工具索引不入档；文件读写由 Persistence 统一，记录写入/读取耗时与体积估算。

---

## 12. 回归脚本（人工/录屏）

1) 启动新存档 → 进行最小交互 → 保存 → 读档 → 打开 Debug“Persistence”查看统计。
2) 点击 `Export JSON` → 清空历史服务内存 → `Import JSON` → 验证 UI 与功能恢复。
3) 手动删改 `ConvKeyIndex` 节点 → 读档 → 验证自动重建索引与差异日志。
4) 构造超长传记条目 → 保存 → 读档 → 验证裁剪与 `truncated=true` 标记日志。
 5) 工具索引：删除索引文件 → 进入地图触发重建；修改 provider/model → 标记过期并后台重建；面板显示新指纹。
6) （移除）FullVectors 入档相关回归；改为检查索引文件重建路径与指纹更新。

---

## 13. 风险与缓解

- 存档体积膨胀：仅“最终输出”入档；对 Recap/传记设字数预算；面板显示节点大小指导调参。
- 写入中断/异常：节点级隔离；失败节点下次可继续；日志留痕便于复现。
- ID 漂移/改名：统一使用稳定 ID（`ThingID`/`saveInstanceId`/`persona:<name>#<rev>`）；显示名由上层解析器解决。
- 索引失配：读档后自动重建；提供自检按钮与差异统计。

---

## 14. FAQ（常见问题）

- Q：为什么只在 Persistence 层使用 `Scribe`？
  - A：将序列化副作用集中，避免 Verse 依赖污染业务；便于审计与回归。

- Q：旧版存档能直接读取吗？
  - A：P6 不支持 v1 老节点；建议以 v2 新格式开档（与 V4/P10 一致）。

- Q：如何导出/导入快照？
  - A：开启 `EnableDebugExport=true` 后，在 Debug 面板“Persistence”中一键导出/导入 JSON。

- Q：历史索引损坏怎么办？
  - A：读档自动重建；也可在 Debug 面板点击“Rebuild History Indexes”手动修复并查看差异统计。

---

## 15. 变更记录（提交要求）

- 初版（v5-P6）：交付发令员-总工程师-专家模型、节点/快照与 Debug/CI/Gate；不改对外 Contracts。
- 后续修改：新增字段需向后兼容；更新本文“节点/快照/步骤/Gate”并附自测与录屏。

---

### 附录 A：最小示例（快照导出/写回）

```csharp
// HistoryService 内部（仅示意）
public HistoryState ExportSnapshot()
{
    _lock.EnterReadLock();
    try {
        return new HistoryState {
            Conversations = new Dictionary<string, ConversationRecord>(_conversations),
            ConvKeyIndex = new Dictionary<string, List<string>>(_convKeyIndex),
            ParticipantIndex = new Dictionary<string, List<string>>(_participantIndex)
        };
    } finally { _lock.ExitReadLock(); }
}

public void ImportSnapshot(HistoryState state)
{
    _lock.EnterWriteLock();
    try {
        _conversations = state.Conversations ?? new();
        _convKeyIndex = state.ConvKeyIndex ?? new();
        _participantIndex = state.ParticipantIndex ?? new();
        RebuildIndexesIfNeeded();
    } finally { _lock.ExitWriteLock(); }
}
```

> 以上附录为理解用示例，不改变本阶段“唯一 Scribe 层在 `PersistenceService`”的纪律。


