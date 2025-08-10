# P10 施工方案 — 历史记录与“前情提要”系统

文档状态：施工计划（实施前置）
进度：M1–M4 已完成，当前推进 M5（账本 v2 切换）

## 0. 背景与动机
- 历史对话必须与存档强一致，支持加载/查看/编辑；Embedding 向量严禁进入存档（铁律）。
 - P9 计划中的前 S5（历史懒索引/异步索引 + 时间线）迁移到本 P10，以更系统化地设计“历史记录 + 前情提要 + 固定提示 + 人物传记 + 关联对话”整体能力，并提供独立 UI 管理。

目标：
- 仅记录“最终输出”的历史（用户/AI/工具的最终话语），去掉中间过程。
- 每 N 轮生成一次“总结”（非流式），每 10 轮将总结叠加成“前情提要”字典（按 conversationId 归档，长度可配，1=仅最新，0/负数=无限）。
- 历史、前情提要、固定提示、人物传记（段落型字典）均持久化为文本；Embedding/RAG 仅在运行时按需构建与使用。
- 支持多人对话，并允许任意参与者子集调取其相关历史作为提示词注入。
- 为每个小人提供“固定提示词”（不可忘内容），并支持 1v1 的“人物传记（段落型字典，非对话形式，可编辑）”。

非目标：
- 不改变对外 Contracts（保持 IOrchestrationService 等稳定接口）。
- 不要求一次性完成高级规划/并发；以最小可用为先，逐步增强。

## 1. 概念与标识（ID 体系）
为保证跨读档与游戏会话的稳定性，统一采用“命名空间:主体键”的复合键：
- pawn:<loadId>（RimWorld Pawn，用 ThingID/GetUniqueLoadID）
- thing:<loadId>（其它物件）
- faction:<loadId> / settlement:<loadId> 等世界对象
- player:<saveInstanceId>（玩家，无天然 ID；首次生成 GUID 并随存档持久化）
- persona:<name>#<rev>（虚拟人格，编辑后 rev+1）
- agent:<guid>（AI 控制台/系统虚拟体）

同时维护“显示名解析器”和“最后可见名缓存”，在目标不在场时仍能展示友好名称。

## 2. 数据模型（文本持久化，Embedding 不入档）
- ConversationEntry：{ SpeakerId, Content, Timestamp }（仅最终输出）
- ConversationRecord：{ ConversationId: guid, ParticipantIds: string[], Entries: ConversationEntry[] }
- HistoricalContext：按参与者集合（convKey）聚合出的主线/背景
- RecapDictionary（前情提要字典）：滚动窗口的结构化条目列表（如 { id, text, createdAt }），索引键=conversationId
- FixedPrompts：固定提示词文本，索引键=PawnId（单个 NPC）
- BiographyDictionary（人物传记字典）：索引键=PawnId 的“段落型、条目化”字典（如 { id, text, createdAt, source }）

会话键 convKey 规则：对参与者集合排序后以 “|” 连接：convKey = join('|', sort(participantIds))。

2.1 主键与二级索引（会话账本 v2）
- 主键：ConversationId（GUID） → ConversationRecord
- 二级索引：
  - convKey → List<ConversationId>
  - participantId → List<ConversationId>（“关联记录索引”，供 UI 快速列举参与过的会话）

## 3. 服务与接口（Core 内部）

### 3.1 IParticipantIdService（ID 统一）
- FromVerseObject(object verseObj) → string id（pawn/thing/faction 等）
- GetPlayerId() → string（player:<saveInstanceId>）
- ForPersona(string name, int rev) → string（persona:<name>#<rev>）
- GetDisplayName(string id) → string（带最后可见名缓存）

### 3.2 History（会话账本 v2）
- Conversation 作为独立字典：conversationId → ConversationRecord
- 提供：
  - CreateConversation(participants: IReadOnlyList<string>) → conversationId
  - AppendEntryAsync(conversationId, entry)
  - GetConversationAsync(conversationId)
  - FindByConvKey(convKey) → List<conversationId>
  - ListByParticipant(participantId) → List<conversationId>
  - EditEntryAsync(conversationId, entryIndex, newContent) / DeleteEntryAsync / RestoreEntryAsync
  - 便捷入口：GetHistoryAsync(participants)（内部通过 convKey 查询并汇总主线/背景）
  - 事件：OnEntryRecorded(conversationId, entry)

### 3.3 Recap（前情提要，独立服务）
- 键=conversationId（≥2 个参与者）
- 来源：历史记录（仅最终输出），可叠加/可覆盖，UI 可编辑重排
- 自动化：保留每 N 轮与“每 10 轮叠加”；可选在“每个象限（Quadrum=15 天）”结束时追加一次全局压缩
- API：Get/Update/Remove/Reorder/Rebuild（后台执行，受预算/时延护栏）

### 3.4 Biography（人物传记，独立服务）
- 键=PawnId（单个小人）
- 周期：每个象限（Quadrum=15 天）自动总结一次（调度由 Scheduler 驱动）
- 来源：对话历史摘录、小人行动 Log、幼年/成年性格、Trait 标签（夜猫子/乐天派/沉鱼落雁 等）
- 产出：段落型字典（非对话）供提示组装，UI 可增删改与重排

### 3.5 FixedPrompts（固定提示词，独立服务）
- 键=PawnId（单个 NPC）
- 主存以 PawnId 为键；可选提供 convKey 覆盖层用于特定会话上下文（如某对话线程中的临时规则）。当同一 NPC 既存在 PawnId 主存项又存在 convKey 覆盖项时，编排注入以 convKey 覆盖项优先。
- 玩家为每个 NPC 设置的“不可遗忘”文本；仅在玩家↔NPC 场景注入

### 3.6 Relations Index（关联记录索引，独立或从 History 暴露）
- 键=participantId → 值=List<conversationId>
- 在 History 发生写入时增量维护；供 UI 直接用参与者维度列举会话
- 建议抽象为 `IRelationsIndexService`（或由 History 暴露只读接口），以便 UI 解耦于账本内部结构。

### 3.7 IPromptAssemblyService（提示词组装）
输入：participantIdSet、预算参数、策略配置。
输出：systemPrompt（或片段列表）。
组装顺序（建议）：
1) Persona（若含 persona:<name>#<rev>）
2) 固定提示词（按 Pawn 粒度，参与者拼接，去重/裁剪）
3) 人物传记段落（仅 1v1 player↔pawn，按需要采样/裁剪）
4) 前情提要字典（当前 conversationId，取最近 K 条，总长度受限；若当前不存在会话则跳过）
5) 近期历史最终输出片段（时间采样）
6) 其它上下文（可选：任务/世界状态）
注意：以上拼接仅用于提示注入，不会写回历史账本。

## 4. 配置项（CoreConfig.History 新增/调整）
```json
History: {
  SummaryEveryNRounds: 5,          // 每N轮生成一次总结
  RecapUpdateEveryRounds: 10,      // 每10轮叠加至“前情提要”
  RecapDictMaxEntries: 20,         // 1=仅保留最新；0或负数=无限
  RecapMaxChars: 1200,             // 单条前情提要最大长度
  HistoryPageSize: 100,            // UI 分页
  MaxPromptChars: 4000,            // 组装提示的总长度预算
  UndoWindowSeconds: 3,            // 删除撤销窗口（秒）
  Budget: {
    MaxLatencyMs: 5000,
    MonthlyBudgetUSD: 5.0
  }
}
```
设置面板提供上述可视化控件；保存后热生效。人物传记/象限自动总结由内部调度驱动，默认开启。

## 5. UI 设计：历史管理窗体（5 个 Tab）
加载键：对话参与者 ID 集合（支持手动录入/选择器）。

- Tab1 历史记录：按 convKey → 列举会话，再选中具体 conversationId 加载“最终输出”；支持逐条编辑/删除/撤销，分页与搜索。
- Tab2 前情提要：展示/编辑“每N轮总结 + 每10轮叠加”的字典项；支持重排/删除/一键重述。
- Tab3 固定提示词：按 PawnId 管理不可遗忘文本；支持新增/编辑/删除；关联 ID 显示名。
- Tab4 关联对话：基于 Relations Index（participantId → List<conversationId>）展示其参与过的所有会话；点击切换加载。
- Tab5 人物传记：按 PawnId 展示/编辑“段落型字典（非对话形式）”，支持新增/编辑/删除与重排；仅在 1v1（player↔pawn）时注入编排。

## 6. 运行流程（最小实现）
1) 创建会话：CreateConversation(participants) → conversationId
2) 记录：AppendEntryAsync(conversationId, entry)（仅最终输出）
3) 计数与总结：IRecapService 监听 OnEntryRecorded(conversationId)，基于 conversationId 直接计数与更新，按 N 轮/每 10 轮/每象限触发
4) 提示组装：会话开始时，IPromptAssemblyService 收集固定提示词（按 Pawn）/人物传记（按 Pawn）/前情提要（按 conversationId）/近期历史片段，裁剪后注入 system 提示（策略侧调用）
5) Embedding：仅对上述文本在运行期懒索引，不入档；更换配置或存档后可重建。

## 7. 持久化策略（全新，不兼容旧版）
- 历史账本（v2）：conversationId → ConversationRecord；同时持久化二级索引（ConvKeyIndex：convKey → List<conversationId>，ParticipantIndex：participantId → List<conversationId>）
- 前情提要（conversationId）、固定提示词（pawnId）、人物传记（pawnId）：全部文本化入档
- Embedding/RAG：仅运行期构建与缓存，严禁入档
- 玩家 ID（player:<saveInstanceId>）与 agent:<guid> 首次生成并随档持久化

7.1 存档节点定义
- ConversationsNode（conversationId → ConversationRecord）
- ConvKeyIndexNode（convKey → List<conversationId>）
- ParticipantIndexNode（participantId → List<conversationId>）
- RecapNode（conversationId → List<RecapSnapshotItem>）
- FixedPromptsNode（pawnId → text）
- BiographiesNode（pawnId → List<BiographyItem>）
- PersonaBindingsNode（pawnId → personaName#rev）

> 说明：不读取/不迁移 v1 旧节点；旧版存档不保证可加载，建议以 v2 新版开始新存档。

## 8. DI 注册与依赖
- IParticipantIdService → ParticipantIdService
- History（会话账本 v2）→ HistoryService（内部维护双索引）
- IRecapService → RecapService（依赖 ILLMService、History、ISchedulerService）
- IBiographyService → BiographyService
- IFixedPromptService → FixedPromptService
- IRelationsIndexService → RelationsIndexService（或由 History 暴露只读接口）
- IPromptAssemblyService → PromptAssemblyService
- IPersonaBindingService → PersonaBindingService

## 9. 验收标准（Gate）
1) 历史账本以 conversationId 为主键，二级索引可用（convKey/participantId）
2) UI 可在 convKey 下列举多个会话并加载具体 conversationId；编辑/删除/撤销生效并入档
3) 前情提要可按 N 轮/每 10 轮/每象限自动更新，UI 可编辑/重排/删除，入档
4) 固定提示词以 pawnId 为键管理（支持 convKey 覆盖层，覆盖优先）；人物传记以 pawnId 为键管理；二者仅在玩家↔NPC 场景注入
5) 关联记录索引可按 participantId 列举其参与过的会话
6) Embedding 不入档；更换档或配置后可懒重建，无明显加载抖动
7) 提示词组装总长度不超过 MaxPromptChars，并可在日志/Debug 面板预览裁剪信息

## 10. 任务与里程碑（含进度）
- M1：接口与骨架 （完成）
  - 定义并注册 IParticipantIdService、IRecapService、IPromptAssemblyService
  - HistoryService 扩展只读/写入/编辑/子集检索骨架
- M2：总结与字典 （完成）
  - RecapService：N 轮总结 + 10 轮叠加；后台异步与护栏
  - 设置面板新增 History 配置项；热生效
- M3：UI 最小实现（5 Tab） （完成）
  - 历史记录/前情提要/固定提示词/关联对话/人物传记（段落型字典） 的只读→可编辑
  - 加载键（ID 集合）选择与回填
- M4：改动较大，且十分重要，详见下方详细设计：14. M4 人格服务 + 提示词服务改造（细化）。 （完成）
  - 已实现：
    - 人格绑定服务 `IPersonaBindingService` 与 UI 绑定面板（支持绑定/解绑/列表），并在 `CommandAsync` 强制校验“玩家↔NPC 且已绑定人格”。
    - `IPromptAssemblyService` 升级为 `BuildSystemPromptAsync(participantIds, mode, userInput, locale)`，统一基于 `IPromptComposer` + 模板组装 Chat/Command 提示，发布 Prompt 审计事件。
    - 人格会话入口改为调用组装服务（不再在入口内手工拼接）；策略层保持 `personaSystemPrompt` 透传，职责清晰。
    - 设置页增加 Chat/Command 段落 Include 开关与条数配置，保存后热生效。
    - 模板热重载：检测母版/覆盖文件时间戳，支持 Debug 面板“Reload Prompts”按钮强制刷新。

- M5：会话账本 v2 全面翻新（不兼容旧版）
  - 引入 conversationId 主键；新增 convKey/participantId 双索引
  - 历史 API 全面改造为以 conversationId 为中心；保留面向参与者集合的便捷查询但不作为主存
  - Relations Index 抽象为独立服务供 UI 使用；取消旧存档迁移，采用全新存档格式
  - 固定提示词切换为以 pawnId 为主存键（支持可选 convKey 覆盖层，覆盖优先）

---

## 13. M5 接口草案与迁移细则（细化）

### 13.1 数据模型（新增/调整）

- ConversationEntry（不变）：{ SpeakerId, Content, Timestamp }
- ConversationRecord（新增，内部）：
  - ConversationId: string（guid）
  - ParticipantIds: IReadOnlyList<string>
  - Entries: IReadOnlyList<ConversationEntry>
- 二级索引（内部持久化）：
  - ConvKeyIndex: Dictionary<string convKey, List<string conversationId>>
  - ParticipantIndex: Dictionary<string participantId, List<string conversationId>>

### 13.2 服务接口（签名草案）

说明：对外 Contracts 仍保持 `IHistoryQueryService.GetHistoryAsync(participantIds)` 兼容；以下为 Core 内部接口/实现扩展。

```csharp
// History（会话账本 v2，内部写）
internal interface IHistoryWriteService
{
    // v2 新增
    string CreateConversation(IReadOnlyList<string> participantIds);
    Task AppendEntryAsync(string conversationId, ConversationEntry entry);
    Task<ConversationRecord> GetConversationAsync(string conversationId);
    Task<IReadOnlyList<string>> FindByConvKeyAsync(string convKey);
    Task<IReadOnlyList<string>> ListByParticipantAsync(string participantId);

    // 现有编辑能力改为按 conversationId
    Task EditEntryAsync(string conversationId, int entryIndex, string newContent);
    Task DeleteEntryAsync(string conversationId, int entryIndex);
    Task RestoreEntryAsync(string conversationId, int entryIndex, ConversationEntry entry);

    // 兼容保留（内部实现通过二级索引映射）
    Task<HistoricalContext> GetHistoryAsync(IReadOnlyList<string> participantIds);

    // 事件：写入后触发（改为 conversationId）
    event Action<string /*conversationId*/, ConversationEntry> OnEntryRecorded;
}

// Recap（键=conversationId）
internal interface IRecapService
{
    IReadOnlyList<RecapSnapshotItem> GetRecapItems(string conversationId);
    bool UpdateRecapItem(string conversationId, string itemId, string newText);
    bool RemoveRecapItem(string conversationId, string itemId);
    bool ReorderRecapItem(string conversationId, string itemId, int newIndex);
    Task RebuildRecapAsync(string conversationId, CancellationToken ct = default);

    // 快照（持久化）
    IReadOnlyDictionary<string /*conversationId*/, IReadOnlyList<RecapSnapshotItem>> ExportSnapshot();
    void ImportSnapshot(IReadOnlyDictionary<string /*conversationId*/, IReadOnlyList<RecapSnapshotItem>> snapshot);
}

// Biography（键=PawnId）
internal interface IBiographyService
{
    IReadOnlyList<BiographyItem> ListByPawn(string pawnId);
    BiographyItem Add(string pawnId, string text);
    bool Update(string pawnId, string itemId, string newText);
    bool Remove(string pawnId, string itemId);
    bool Reorder(string pawnId, string itemId, int newIndex);

    // 快照（持久化）
    IReadOnlyDictionary<string /*pawnId*/, IReadOnlyList<BiographyItem>> ExportSnapshot();
    void ImportSnapshot(IReadOnlyDictionary<string, IReadOnlyList<BiographyItem>> snapshot);
}

// FixedPrompts（键=PawnId；支持 convKey 覆盖层）
internal interface IFixedPromptService
{
    // 主存（按 PawnId）
    string GetByPawn(string pawnId);
    void UpsertByPawn(string pawnId, string text);
    bool DeleteByPawn(string pawnId);
    IReadOnlyDictionary<string /*pawnId*/, string> GetAllByPawn();

    // 覆盖层（可选，按 convKey；用于特定会话上下文临时覆盖）
    string GetConvKeyOverride(string convKey);
    void UpsertConvKeyOverride(string convKey, string text);
    bool DeleteConvKeyOverride(string convKey);
    IReadOnlyDictionary<string /*convKey*/, string> GetAllConvKeyOverrides();

    // 快照（持久化）
    IReadOnlyDictionary<string /*pawnId*/, string> ExportSnapshot();
    void ImportSnapshot(IReadOnlyDictionary<string, string> snapshot);
}

// Relations Index（独立服务或由 History 暴露只读接口）
internal interface IRelationsIndexService
{
    // 列举某参与者参与过的所有会话（conversationId）
    Task<IReadOnlyList<string>> ListConversationsByParticipantAsync(string participantId);
}
```

注：现实现已支持 convKey 维度的部分能力（M3）。M5 起主语切换为单体键（FixedPrompts=PawnId，Biography=PawnId），不提供旧版存档结构的读取或迁移。

### 13.3 持久化结构（新增/变更节点）

- ConversationsNode（conversationId → ConversationRecord）
- ConvKeyIndexNode（convKey → List<conversationId>）
- ParticipantIndexNode（participantId → List<conversationId>）
- RecapNode（conversationId → List<RecapSnapshotItem>）
- FixedPromptsNode（pawnId → text）
- BiographiesNode（pawnId → List<BiographyItem>）

### 13.4 全新实现（不兼容旧版）

- v2 仅支持以 conversationId 为主键的账本与双索引；不提供从 v1 旧结构到 v2 的读取或迁移逻辑。
- FixedPrompts 仅以 pawnId 为键（支持可选 convKey 覆盖层）；Biography 仅以 pawnId 为键；Recap 以 conversationId 为键；所有旧结构一律忽略。
- 文档与 UI 全面按 v2 设计编写；发布说明中明确“需要新开存档”。

### 13.5 调度与自动总结（Quadrum=15天）

- 人物传记：在 `GameComponent.Update`/调度器中检测 Quadrum 变更（或 `DaysPassed % 15 == 0`），为所有 Pawn 触发 `IBiographyService` 的增量总结（受预算与超时护栏）
- 前情提要：保留每 N 轮/每 10 轮逻辑，新增“每象限末尾”的补偿压缩（可配）

### 13.6 UI 调整（History Manager）

- Tab1：convKey → conversationId 列表选择器（必须）；编辑/删除/撤销基于 conversationId
- Tab3：固定提示词切换为按 PawnId 编辑；可显示 convKey 覆盖层（如启用覆盖功能）
- Tab5：人物传记切换为按 PawnId 编辑；在 1v1 会话下注入到编排

### 13.7 验收与测试

- 新建存档下，能够创建/加载以 conversationId 为主键的会话，索引完整；UI 行为符合预期
- 新建/编辑/删除记录后，ConvKeyIndex 与 ParticipantIndex 均正确更新
- 人物传记在象限切换时自动追加段落；前情提要在 N 轮/十轮/象限策略下均可观测
- 编排注入顺序正确且不污染历史；提示长度裁剪符合上限

---

## 14. M4 人格服务 + 提示词服务改造（细化）

### 14.1 人格服务作为唯一对话入口

- 会话类型：
  - Chat（闲聊，无 Tool Calls）：轻量提示词 → LLM 网关；默认不写历史（可配置）。
  - Command（命令，包含 Tool Calls）：完整提示词 → 编排（Orchestration/策略/RAG/工具）→ 仅写“最终输出”到历史；仅允许玩家↔NPC，且该 NPC 已绑定人格。

- 依赖服务（素材来源）：
  - 历史（最终输出）、前情提要（conversationId）、固定提示词（单 NPC）、人物传记（单 Pawn）、关联记录（participantId → conversationId 列表）。

- 现有人格管理 UI/CRUD/绑定保持不变。

### 14.2 提示词服务（分段组装 + 本地化模板）

- 新增 IPromptComposer（供内部使用）：
  - Begin(templateKey: chat|command, locale)
  - Add(labelKey, material: string|IEnumerable<string>) // 多次调用按顺序叠加
  - Build(maxChars, out audit) → string // 输出最终 system 提示，返回审计信息（各段长度、裁剪）
  - 审计结构 PromptAudit：{ segments: [{ labelKey, addedChars, truncated }], totalChars }

- IPromptAssemblyService 调整为调用 IPromptComposer，向外仍暴露：
  - BuildSystemPromptAsync(participantIds, mode: Chat|Command, userInput, locale) → string
  - mode 确定要注入的段落组合；最后一段固定 Add("user_utterance", $"玩家说：{userInput}")
  - 状态：已落地；并在组装完成后发布 Prompt 审计事件（Debug 面板可见）。

- 模板/本地化（母版 + 覆盖文件）：
  - 母版（只读，随 Mod 发布）：`Resources/prompts/<locale>.json`
  - 覆盖（可写，用户自定义）：`Config/RimAI/Prompts/<locale>.user.json`
  - 加载策略：启动/读档时加载一次；如检测覆盖文件更新（时间戳/哈希）则热重载；用户修改只写覆盖文件，不污染母版。Debug 面板提供“Reload Prompts”按钮手动触发刷新。
  - 模板结构建议：
    ```json
    {
      "version": 1,
      "locale": "zh-Hans",
      "templates": {
        "chat": ["persona", "fixed_prompts", "recap", "recent_history", "user_utterance"],
        "command": ["persona", "fixed_prompts", "biography", "recap", "related_history", "user_utterance"]
      },
      "labels": {
        "persona": "[人格]",
        "fixed_prompts": "[固定提示词]",
        "biography": "[人物传记]",
        "recap": "[前情提要]",
        "recent_history": "[近期历史]",
        "related_history": "[相关历史]",
        "user_utterance": "玩家说：{text}"
      }
    }
    ```

### 14.3 Chat 与 Command 的素材差异（默认模板）

- Chat（轻）：Persona + FixedPrompts（NPC）+ Recap（当前 conversationId，近 K 条，若无会话则跳过）+ 近期历史（少量条） + 用户输入
- Command（重）：Persona + FixedPrompts（NPC）+ Biography（1v1）+ Recap（当前 conversationId，近 K 条）+ 相关历史（来自 Relations Index，限制条数/总字数）+ 用户输入
- 历史写入：仅 Command 写入“最终输出”（用户/AI/工具），不写入过程；触发 Recap 计数/叠加。

### 14.4 入口接口草案（不改对外 Contracts）

```csharp
internal interface IPersonaConversationService
{
    // 闲聊（无工具）
    IAsyncEnumerable<Result<UnifiedChatChunk>> ChatAsync(
        IReadOnlyList<string> participantIds,
        string personaName,
        string userInput,
        PersonaChatOptions options);

    // 命令（工具/编排）
    IAsyncEnumerable<Result<UnifiedChatChunk>> CommandAsync(
        IReadOnlyList<string> participantIds,
        string personaName,
        string userInput,
        PersonaCommandOptions options);
}

internal sealed class PersonaChatOptions { public string Locale; public bool Stream=true; public bool WriteHistory=false; }
internal sealed class PersonaCommandOptions { public string Locale; public bool Stream=true; public bool RequireBoundPersona=true; public bool WriteHistory=true; }
```

### 14.5 约束与校验

- Command：必须为玩家↔NPC 且 NPC 已绑定人格；否则拒绝并提示。
- Prompt 长度：严格裁剪到 MaxPromptChars；每段材质也有独立预算（见配置）。
- 事件与审计：PromptAudit 与阶段事件通过 EventBus 广播，Debug 面板可查看。

### 14.6 UI 改动

- 在小人卡或相关菜单增加“历史记录”按钮 → 打开 History Manager（沿用现有 UI）。
- 人格管理 UI 增加“绑定 NPC→Persona”入口，打开绑定面板以完成绑定/解绑操作。

### 14.7 Gate（M4）

- Chat/Command 两条入口可用；Command 仅在玩家↔NPC 且绑定人格时放行。✅
- Prompt 由模板（本地化 JSON）+ 标签化素材构建；末段固定“玩家说：{输入}”。✅
- Debug 面板可查看 Prompt 审计与裁剪信息；支持手动“Reload Prompts”。✅

---

## 15. 可配置项清单（玩家可自定义）

注：以下为新增/整合项；旧有设置（工具匹配、阈值、规划器、Embedding 等）维持现有章节。

### 15.1 历史/前情/传记
- History.SummaryEveryNRounds（int，默认5）
- History.RecapUpdateEveryRounds（int，默认10）
- History.RecapDictMaxEntries（int，默认20，1=仅最新，≤0=无限）
- History.RecapMaxChars（int，默认1200）
- History.HistoryPageSize（int，默认100）
- History.MaxPromptChars（int，默认4000）
- History.UndoWindowSeconds（int，默认3）
- History.RecapAutoOnQuadrumEnd（bool，默认true）
- History.BiographyAutoOnQuadrumEnd（bool，默认true）
- History.BiographyMaxEntries（int，默认50）

### 15.2 提示词/模板
- Prompt.Locale（string，默认"zh-Hans"）
- Prompt.TemplateChatKey（string，默认"chat"）
- Prompt.TemplateCommandKey（string，默认"command"）
- Prompt.MasterPath（string，默认 Resources/prompts/{locale}.json）
- Prompt.UserOverridePath（string，默认 Config/RimAI/Prompts/{locale}.user.json）
- Prompt.Segments.Chat：
  - IncludePersona（bool，默认true）
  - IncludeFixedPrompts（bool，默认true）
  - IncludeRecap（bool，默认true）
  - IncludeRecentHistory（bool，默认true）
  - RecentHistoryMaxEntries（int，默认6）
- Prompt.Segments.Command：
  - IncludePersona（bool，默认true）
  - IncludeFixedPrompts（bool，默认true）
  - IncludeBiography（bool，默认true，仅1v1时生效）
  - IncludeRecap（bool，默认true）
  - IncludeRelatedHistory（bool，默认true）
  - RelatedMaxConversations（int，默认3）
  - RelatedMaxEntriesPerConversation（int，默认5）

### 15.3 对话入口（人格）
- Persona.Chat.Enabled（bool，默认true）
- Persona.Command.Enabled（bool，默认true）
- Persona.Command.RequireBoundPersona（bool，默认true）
- Persona.Command.WriteHistory（bool，默认true）
- Persona.Chat.WriteHistory（bool，默认false）
- Persona.Streaming.Default（bool，默认true）

### 15.4 预算与护栏
- History.Budget.MaxLatencyMs（int，默认5000）
- History.Budget.MonthlyBudgetUSD（double，默认5.0）
- Prompt.Budget.PerSegmentMaxChars：
  - Persona（int，默认1200）
  - FixedPrompts（int，默认800）
  - Biography（int，默认1200）
  - Recap（int，默认1200）
  - RecentHistory（int，默认800）
  - RelatedHistory（int，默认1600）

（实现建议：新增 PromptConfig；设置面板新增“提示词/模板”分节，支持路径/语言与段落预算；保存后热生效，并合并母版与覆盖文件。）

## 11. 回滚
- 关闭历史总结与前情提要（禁用 RecapService），保留“仅记录最终输出”。
- 策略端停止注入组装提示（回到 PersonaSystemPrompt + 即时上下文）。

## 12. 风险与缓解
- 成本/时延：总结与叠加放后台并设超时；失败降级为跳过该轮
- 漂移与积累：提供“一键重述”压缩前情提要；UI 可编辑与修正
- ID 稳定性：player:<saveInstanceId> 固定，persona:<name>#<rev> 可追踪版本；显示名缓存避免离场空白

> 本方案与 V4 架构一致：历史入档、Embedding 不入档；提示词组装在编排前注入，工具与 RAG 仍遵循现有策略链与防护。


