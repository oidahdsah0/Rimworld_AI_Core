# RimAI V5 — P7 实施计划（Persona：职务/传记/意识形态/固定提示词）

> 目标：在不引入 Event Aggregator 的前提下，一次性交付“人格域（Persona）”的最小闭环：职务（名称/描述）的设置与查询、个人传记的生成与查询、意识形态的生成与查询、固定提示词的设置与查询。本文档为唯一入口，无需查阅旧版文档即可完成落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 人格严格以实体唯一键绑定：仅允许 `pawn:<loadId>` 与 `thing:<loadId>`（如 AI 服务器）。无任何其他键（如 `convKey`）。
  - 四类能力：
    1) 职务设置与查询（职务名/描述；支持“从职务名非流式生成描述”）
    2) 个人传记（段落化）的生成与查询（非流式生成草案 → 挑选/编辑/保存）
    3) 意识形态（世界观/价值观/行为准则/性格特质）的生成与查询（非流式）
    4) 固定提示词的设置与查询（手动维护，可一键从现有文本萃取草案）
  - Persona Block 组合：按段序与预算生成“人格段”文本（供 P8 Prompt 组装器直接消费）。
  - Debug/可观测：提供 P7 专用面板与统一日志；生成类操作可取消、失败兜底为手动编辑。

- 非目标（后续阶段处理）
  - 不引入 Event Aggregator/Stage/Act/Kernel（V5 已取消该模块）。
  - 不进行对话历史聚合/写入（历史域继续“仅最终输出”原则，另行推进）。
  - 不实现工具授权/自动化编排；不新增对外 Contracts（维持 P1 的“只读配置快照”为最小对外面）。

---

## 1. 架构总览（全局视角）

- 定位：Persona 属于“数据与素材域”，为 UI 与未来的 Prompt 服务提供稳定的人格素材；不直接触达 LLM 以外的外部系统。
- 依赖边界：
  - P1：`IConfigurationService`（内部配置/预算/模板/语言，只读）
  - P2：`ILLMService`（唯一 LLM 通道，生成类调用一律非流式）
  - P3：`ISchedulerService`/`IWorldDataService`（主线程化只读世界快照）
  - P6：`IPersistenceService`（唯一 Scribe 层，节点读写与导入导出）
- 不依赖：Orchestration/Tooling/Stage/Event Aggregator（本阶段完全无耦合）。
- 入口：对上仅暴露一个门面 `IPersonaService`；内部聚合四个专业子服务（Job/Biography/Ideology/FixedPrompt）。

---

## 2. 核心原则与纪律

1) 键唯一：索引键严格为 `EntityId := "pawn:<loadId>" | "thing:<loadId>"`；使用 Cursor 内置工具的 Gate 检查，确保 Persona 代码中 `convKey`/其它键出现次数为 0。
2) 非流式：所有生成类调用一律使用 `ILLMService.GetResponseAsync`；后台代码禁止 `StreamResponseAsync`。
3) 世界访问最小面：仅 `IWorldDataService` 访问 Verse；Persona 模块内部不得 `using Verse`/`Scribe`。
4) 配置快照：通过 `IConfigurationService.Current` 获取不可变快照，支持热重载事件订阅。
5) 持久化隔离：节点读写仅在 `IPersistenceService` 内部；Persona 模块仅导出/导入快照对象。
6) 可编辑优先：生成仅产出草案，最终文本以玩家编辑为准；失败时不写入半成品。

---

## 3. 数据模型与持久化节点（版本与结构）

> 节点命名与 P6 对齐，均带版本后缀；仅键=EntityId（`pawn:<loadId>`/`thing:<loadId>`）。

- 新增/沿用节点
  - `RimAI_PersonaJobV1`：`entityId → { Name, Description, UpdatedAtUtc }`
  - `RimAI_BiographiesV1`：`entityId → List<BiographyItem { Id, Text, Source, UpdatedAtUtc }]`
  - `RimAI_PersonalBeliefsV1`：`entityId → { Worldview, Values, CodeOfConduct, TraitsText, UpdatedAtUtc }`
  - `RimAI_FixedPromptsV1`：`entityId → string Text`

- 内部快照（POCO，严禁 Verse 类型）
```csharp
internal sealed class PersonaJobSnapshot { public string Name; public string Description; public DateTime UpdatedAtUtc; }
internal sealed class BiographyItem { public string Id; public string Text; public string Source; public DateTime UpdatedAtUtc; }
internal sealed class IdeologySnapshot { public string Worldview; public string Values; public string CodeOfConduct; public string TraitsText; public DateTime UpdatedAtUtc; }
internal sealed class FixedPromptSnapshot { public string Text; public DateTime UpdatedAtUtc; }

internal sealed class PersonaRecordSnapshot {
  public string EntityId; public PersonaJobSnapshot Job; public List<BiographyItem> Biography;
  public IdeologySnapshot Ideology; public FixedPromptSnapshot FixedPrompts; public string Locale;
}
```

- 读取策略
  - 缺失节点视为空；读档后通过门面原子写回内存状态。
  - 文本长度超限按配置裁剪并记录 `truncated=true` 的审计日志（不打印正文）。

---

## 4. 接口契约（内部 API）

> Persona 不进入 `RimAI.Core.Contracts`（对外层）；以下接口位于 Core 内部命名空间。

```csharp
// 门面：唯一对上入口
internal interface IPersonaService {
  // 读取/写入
  PersonaRecordSnapshot Get(string entityId);
  void Upsert(string entityId, Action<PersonaRecordEditor> edit); // 原子编辑器（见下）
  void Delete(string entityId);

  // 组合 Persona Block（供 P8 Prompt 组装使用）
  string ComposePersonaBlock(string entityId, PersonaComposeOptions options, out PersonaComposeAudit audit);

  // 轻量领域事件（非全局 Event Aggregator）
  event Action<string /*entityId*/, string[] /*changedFields*/> OnPersonaUpdated;
}

internal sealed class PersonaRecordEditor {
  public void SetJob(string name, string description);
  public void SetFixedPrompt(string text);
  public void AddOrUpdateBiography(string id, string text, string source);
  public void RemoveBiography(string id);
  public void SetIdeology(string worldview, string values, string codeOfConduct, string traitsText);
}

internal sealed class PersonaComposeOptions {
  public string Locale; public int MaxTotalChars = 4000; 
  public int MaxJobChars = 600, MaxFixedChars = 800, MaxIdeologySegment = 600, MaxBioPerItem = 400, MaxBioItems = 4;
  public bool IncludeJob = true, IncludeFixedPrompts = true, IncludeIdeology = true, IncludeBiography = true;
}

internal sealed class PersonaComposeAudit { public int TotalChars; public List<(string seg, int len, bool truncated)> Segments; }

// 专业服务（内部）
internal interface IPersonaJobService {
  PersonaJobSnapshot Get(string entityId);
  void Set(string entityId, string name, string description);
  // 非流式：从职务名生成描述（可选）
  // 生成“从职务名推导描述”的接口已取消；仅保留 Get/Set 与其它生成功能
}

internal interface IBiographyService {
  IReadOnlyList<BiographyItem> List(string entityId);
  void Upsert(string entityId, BiographyItem item); void Remove(string entityId, string id);
  Task<List<BiographyItem>> GenerateDraftAsync(string entityId, CancellationToken ct = default);
}

internal interface IIdeologyService {
  IdeologySnapshot Get(string entityId);
  void Set(string entityId, IdeologySnapshot s);
  Task<IdeologySnapshot> GenerateAsync(string entityId, CancellationToken ct = default);
}

internal interface IFixedPromptService { FixedPromptSnapshot Get(string entityId); void Set(string entityId, string text); }
```

实现要点：
- 生成类方法一律调用 `ILLMService.GetResponseAsync`（非流式）；所有入口均支持取消与超时（从配置读取默认值）。
- 门面负责合并持久化快照、触发 `OnPersonaUpdated` 与审计统计；四服务各自封装模板与世界快照采样逻辑。

---

## 5. 目录结构与文件

```text
RimAI.Core/
  Source/
    Modules/
      Persona/
        IPersonaService.cs
        PersonaService.cs                 // 门面：聚合四服务，组合 Persona Block
        Job/
          IPersonaJobService.cs
          PersonaJobService.cs            // 生成描述（非流式）+ CRUD + 审计
        Biography/
          IBiographyService.cs
          BiographyService.cs             // 草案生成（非流式）+ 段落 CRUD + 排序
        Ideology/
          IIdeologyService.cs
          IdeologyService.cs              // 四段生成（非流式）+ CRUD
        FixedPrompt/
          IFixedPromptService.cs
          FixedPromptService.cs           // 仅按 entityId 维护整段文本
        Templates/
          zh-Hans.persona.json            // 母版模板（见 §6）
          zh-Hans.persona.user.json       // 覆盖模板（热重载）
      World/
        IWorldDataService.cs              // 依赖（只读）
    UI/DebugPanel/Parts/
      P7_PersonaPanel.cs                  // 生成/预览/保存/导入导出
```

---

## 6. 生成策略与模板（非流式）

- 统一约束
  - 调用 `ILLMService.GetResponseAsync`；禁止 `StreamResponseAsync`；默认超时/重试/断路器沿用 P2。
  - 模板与本地化：母版放置 `Resources/prompts/persona/zh-Hans.persona.json`（随 Mod），用户覆盖位于 `Config/RimAI/Prompts/persona/zh-Hans.persona.user.json`；支持热重载（时间戳/哈希变化）。
  - 段级预算：`Job/FixedPrompts/Ideology/Biography` 各自最大字数，超出裁剪并记录审计。

- 模板结构建议
```json
{
  "version": 1,
  "locale": "zh-Hans",
  "prompts": {
    "jobFromName": "你是角色设定助手。根据职务名生成一段<300字的职责描述，语言简练、具象、避免空话。输入: {jobName}; 背景: {context}",
    "biographyDraft": "你是传记撰写助手。基于信息为角色生成3-5条不超过{maxPerItem}字的传记段落，每条独立成段，避免重复与无证据推断。信息: {facts}",
    "ideology": "你是设定编辑。为角色生成四段文本：世界观/价值观/行为准则/性格特质（各≤{maxSeg}字），语言紧凑一致。信息: {facts}"
  }
}
```

- 提示拼装要点
  - 最小世界快照（见 §7）作为 `{context}`/`{facts}` 注入；缺失字段则弱化提示而不失败。
  - 返回体遵循简文本（不要求 JSON），由服务进行长度裁剪；段落边界用换行或“•”分隔。

---

## 7. 世界数据快照（最小字段）

> 由 `IWorldDataService` 在主线程安全采集；调用点位于各专业服务内部。

- 通用字段：
  - `DisplayName`（实体显示名）
  - `EntityKind`（Pawn|Thing；Thing 用于 AI 服务器等）
  - `FactionName?`，`MapName?`
  - `AgeText?`，`KeyTraits?`（若可得，以 3–5 个标签）
  - `RecentActivitySummary?`（近 1–3 条人类可读摘要，若不可得则省略）

- 采集规则：
  - 任何 Verse 访问均包裹于 `ISchedulerService.ScheduleOnMainThreadAsync`；失败抛 `WorldDataException` 并回退为弱上下文生成。

---

## 8. 配置（CoreConfig.Persona · 内部）

> 通过 `IConfigurationService` 读取，不新增对外 Snapshot 字段。

```json
{
  "Persona": {
    "Locale": "zh-Hans",
    "Budget": {
      "MaxTotalChars": 4000,
      "Job": 600,
      "Fixed": 800,
      "IdeologySegment": 600,
      "BiographyPerItem": 400,
      "BiographyMaxItems": 4
    },
    "Generation": {
      "TimeoutMs": 15000,
      "Retry": { "MaxAttempts": 3, "BaseDelayMs": 400 }
    },
    "Templates": {
      "MasterPath": "Resources/prompts/persona/{locale}.persona.json",
      "UserOverridePath": "Config/RimAI/Prompts/persona/{locale}.persona.user.json",
      "HotReload": true
    },
    "UI": { "EnableExtractFixedFromExisting": true }
  }
}
```

---

## 9. Debug 面板（P7 专用）

- 入口：`P7_PersonaPanel`（仅当 `UI.DebugPanelEnabled=true`）
- 功能按钮：
  - 选择目标 `EntityId`（从当前地图选中 Pawn/Thing 自动解析）
  - 职务：设置名称；“从名称生成描述”（非流式，可取消）；编辑与保存
  - 意识形态：四段显示；“生成/单段重写”；保存
  - 传记：列表增删改/排序；“生成草案（3–5条）”；逐条采纳并保存
  - 固定提示词：整段编辑；“从现有文本萃取草案”
  - Persona Block：预览组合文本与审计（段长度/截断/总长）
- 观测：打印 `[RimAI.Core][P7.Persona]` 日志（开始/结束/耗时/截断比/失败码），不输出敏感正文。

---

## 10. 实施步骤（一步到位）

> 按顺序完成 S1→S12；每步可通过 Debug 面板或日志进行自检；无需查阅其他文档。

- S1：快照与节点
  - 新建 `PersonaJobSnapshot`/`BiographyItem`/`IdeologySnapshot`/`FixedPromptSnapshot` 与 `PersonaRecordSnapshot`。
  - 在 `IPersistenceService` 中新增读写节点：`RimAI_PersonaJobV1`（新增）、`RimAI_BiographiesV1`、`RimAI_PersonalBeliefsV1`、`RimAI_FixedPromptsV1`。

- S2：接口与门面骨架
  - 新建 `IPersonaService` 与 `PersonaService`；实现 `Get/Upsert/Delete/ComposePersonaBlock/OnPersonaUpdated`。
  - `Upsert` 使用“编辑器模式”保证原子性（拉取快照 → 编辑 → 写回 → 触发事件）。

- S3：专业服务骨架
  - `IPersonaJobService` / `IBiographyService` / `IIdeologyService` / `IFixedPromptService` 与对应实现文件。
  - 接线 DI：构造函数注入 `ILLMService`、`IWorldDataService`、`IConfigurationService`。

- S4：模板与本地化
  - 落地 `Templates/zh-Hans.persona.json` 与覆盖文件路径；实现热重载（时间戳/哈希）。
  - 解析并缓存模板；缺失时退回内置母版。

- S5：生成流程（非流式）
  - 职务描述：不再提供“从名称生成描述”的自动接口；建议由玩家手工填写或通过其它管道导入草案。
  - 传记草案：`GenerateDraftAsync(entityId)` → 3–5 条条目（≤ `BiographyPerItem`）；去重/去空。
  - 意识形态：`GenerateAsync(entityId)` → 四段（≤ `IdeologySegment`）；空段跳过。
  - 所有流程：支持取消；失败以 `Result.Error` 映射统一异常并在 UI 提示。

- S6：组合 Persona Block
  - 顺序：`Job` → `FixedPrompts` → `Ideology(四段)` → `Biography(Top-K)`；应用段级预算与总长预算。
  - 返回文本与 `PersonaComposeAudit`；Debug 面板可预览。

- S7：持久化整合
  - 门面在 `Get/Upsert/Delete` 时与 `IPersistenceService` 交互；读档缺失节点视为空。
  - 导入导出（可选）：在 Debug 面板提供 JSON 导入导出（调用 P6 的导出接口）。

- S8：UI 面板实现
  - `P7_PersonaPanel`：按 §9 功能就绪；所有生成按钮非阻塞+可取消；保存后触发 `OnPersonaUpdated`。

- S9：日志与审计
  - 统一前缀 `[RimAI.P7.Persona]`；记录：op、entityId-hash、elapsed、segments、truncated、error。

- S10：DI 注册与启动检查
  - 在 `ServiceContainer.Init()` 注册门面与四服务；启动日志打印“P7 Persona ready”。

- S11：CI/Gate（使用 Cursor 内置工具）
  - 全仓（排除 `Modules/Persistence/**` 与 `Modules/World/**`）：
    - 检查=0：`\bStreamResponseAsync\(`、`\bconvKey\b`、`\bScribe\.|using\s+Verse`。
  - Persona 目录内 检查=0：`\bEventAggregator\b`。

- S12：人工回归脚本（录屏）
  - 选择 Pawn/Thing → 新建 Persona → 设置职务名 → 生成描述 → 编辑保存。
  - 生成传记草案，采纳 3 条；生成意识形态四段；编辑固定提示词；
  - 预览 Persona Block（显示审计）；保存 → 读档 → 所有文本一致；
  - 断网/超时：生成失败有友好提示，面板仍可手动编辑并保存。

---

## 11. 验收 Gate（必须全绿）

- 键与纪律
  - 仅 `pawn:<loadId>`/`thing:<loadId>` 为键；Persona 目录中 `convKey`/其它键 grep=0。
  - 生成一律非流式；Persona 目录中 `StreamResponseAsync(` grep=0。
  - Persona 模块无 `Verse/Scribe` 引用；世界访问仅在 `IWorldDataService` 内。

- 功能完整
  - 职务设置/查询（含“从名称生成描述”）；传记生成/查询；意识形态生成/查询；固定提示词设置/查询。
  - `ComposePersonaBlock` 输出段序正确、受预算约束，并返回审计（段长度/截断/总长）。

- 持久化
  - `RimAI_PersonaJobV1` 节点可读写；读档后与保存前一致；缺失节点不影响其它域。

- UI/可观测
  - 生成类操作可取消、失败提示清晰；无明显卡顿；日志与面板审计信息可见。

---

## 12. CI/Gate（使用 Cursor 内置工具，必须通过）

- Persona 目录（`Source/Modules/Persona/**`）检查=0：
  - `\bStreamResponseAsync\(`、`\bEventAggregator\b`、`\bconvKey\b`、`\bScribe\.|using\s+Verse`。
- 全仓（排除 `Modules/Persistence/**` 与 `Modules/World/**`）检查=0：`\bScribe\.|using\s+Verse`。
- 可选：模板热重载日志包含“persona templates reloaded”。

---

## 13. 风险与缓解

- 生成质量不稳定 → 提供“草案→人工采纳”的编辑流；段级预算与裁剪；可多次生成覆盖。
- 世界快照缺失 → 降级为弱上下文生成；提示“缺少背景信息可能导致泛化描述”。
- 性能/费用 → 非流式短请求；重试+断路；UI 聚合生成次数（例如一次生成传记草案后逐条采纳）。
- 本地化 → 模板母版+覆盖文件；热重载；默认 `zh-Hans`。

---

## 14. FAQ（常见问题）

- Q：为什么不支持流式生成？
  - A：后台/服务型路径统一非流式（P2 纪律），避免 UI 抖动与资源占用；生成文本较短，非流式足够。
- Q：固定提示词支持会话级覆盖吗？
  - A：不支持。P7 严格以实体键为索引；会话级场景提示由后续 Stage/Organizer 解决。
- Q：能否直接对外暴露 Persona 读写接口？
  - A：V5 维持 Contracts 最小面；如需对外访问，后续考虑只读查询接口，当前版本不暴露。

---

## 15. 附录 A：Persona Block 组合规则（示意）

```text
[职务]
{Job.Name}：{Job.Description}

[固定提示词]
{FixedPrompts.Text}

[意识形态]
世界观：{Ideology.Worldview}
价值观：{Ideology.Values}
行为准则：{Ideology.CodeOfConduct}
性格特质：{Ideology.TraitsText}

[人物传记]
- {Biography.Item1}
- {Biography.Item2}
...
```

> 组合时按预算裁剪；空段跳过；段落间以空行分隔，保持可读性。

---

本文件为 V5 P7 唯一权威实施说明。实现与验收以本文为准。


