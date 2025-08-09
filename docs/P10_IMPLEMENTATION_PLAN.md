# P10 施工方案 — 历史记录与“前情提要”系统

文档状态：施工计划（实施前置）

## 0. 背景与动机
- 历史对话必须与存档强一致，支持加载/查看/编辑；Embedding 向量严禁进入存档（铁律）。
- P9 计划中的前 S5（历史懒索引/异步索引 + 时间线）迁移到本 P10，以更系统化地设计“历史记录 + 前情提要 + 固定提示 + 传记 + 关联对话”整体能力，并提供独立 UI 管理。

目标：
- 仅记录“最终输出”的历史（用户/AI/工具的最终话语），去掉中间过程。
- 每 N 轮生成一次“总结”（非流式），每 10 轮将总结叠加成“前情提要”字典（长度可配，1=仅最新，0/负数=无限）。
- 历史、前情提要、固定提示、人物传记均持久化为文本；Embedding/RAG 仅在运行时按需构建与使用。
- 支持多人对话，并允许任意参与者子集调取其相关历史作为提示词注入。
- 为每个小人提供“固定提示词”（不可忘内容），并支持 1v1 的“人物传记”。

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
- Conversation：Entries 列表
- HistoricalContext：按参与者集合（convKey）聚合出的主线/背景
- RecapDictionary（前情提要字典）：滚动窗口的结构化条目列表（如 { id, text, createdAt }）
- FixedPrompts：按 ParticipantId 存储的固定提示词文本
- Biography：按 PawnId（以及 player 1v1）管理的人物传记（格式参考“前情提要”）

会话键 convKey 规则：对参与者集合排序后以 “|” 连接：convKey = join('|', sort(participantIds))。

## 3. 服务与接口（Core 内部）

### 3.1 IParticipantIdService（ID 统一）
- FromVerseObject(object verseObj) → string id（pawn/thing/faction 等）
- GetPlayerId() → string（player:<saveInstanceId>）
- ForPersona(string name, int rev) → string（persona:<name>#<rev>）
- GetDisplayName(string id) → string（带最后可见名缓存）

### 3.2 IHistoryService（扩展）
- RecordEntryAsync(participants: IReadOnlyList<string>, entry: ConversationEntry)
  - 仅写入最终输出（用户/AI/工具均可作为 SpeakerId）
- GetHistoryAsync(participants)
- GetConversationsBySubsetAsync(queryIds: IReadOnlyList<string>)：返回所有满足 queryIds ⊆ conv.Participants 的会话
- EditEntryAsync(convKey, entryIndex, newContent)
- ListConversationKeysAsync(filter?, paging?)
- 事件：OnEntryRecorded(convKey, entry)

### 3.3 IRecapService（总结/叠加）
- OnEntryRecorded(convKey)：内部计数；每 N 轮触发一次 Summarize（非流式 LLM）
- OnEveryTenRounds(convKey)：把最近 10 轮的段落叠加/压缩→写入 RecapDictionary（长度受限，超出按 FIFO 或 LRU 丢弃）
- RebuildRecapAsync(convKey)：UI 触发“一键重述”
实现要点：后台异步执行；预算与超时护栏；失败不阻塞主流程。

### 3.4 IPromptAssemblyService（提示词组装）
输入：participantIdSet、预算参数、策略配置。
输出：systemPrompt（或片段列表）。
组装顺序（建议）：
1) 固定提示词（每个参与者拼接，去重、裁剪）
2) 人物传记（仅 1v1 player↔pawn 时注入）
3) 前情提要字典（按配置取最近 K 条，总长度受限）
4) 相关历史最终输出片段（按时间/相关性采样）
5) 其它上下文（可选：任务状态等）

## 4. 配置项（CoreConfig.History 新增）
```json
History: {
  SummaryEveryNRounds: 5,          // 每N轮生成一次总结
  RecapUpdateEveryRounds: 10,      // 每10轮叠加至“前情提要”
  RecapDictMaxEntries: 20,         // 1=仅保留最新；0或负数=无限
  RecapMaxChars: 1200,             // 单条前情提要最大长度
  HistoryPageSize: 100,            // UI 分页
  MaxPromptChars: 4000,            // 组装提示的总长度预算
  Budget: {
    MaxLatencyMs: 5000,
    MonthlyBudgetUSD: 5.0
  }
}
```
设置面板提供上述可视化控件；保存后热生效。

## 5. UI 设计：历史管理窗体（5 个 Tab）
加载键：对话参与者 ID 集合（支持手动录入/选择器）。

- Tab1 历史记录：按 convKey 加载，展示仅“最终输出”；支持逐条编辑/删除，分页与搜索。
- Tab2 前情提要：展示/编辑“每N轮总结 + 每10轮叠加”的字典项；支持重排/删除/一键重述。
- Tab3 固定提示词：按参与者管理不可遗忘文本；支持新增/编辑/删除；关联 ID 显示名。
- Tab4 关联对话：展示与当前集合有交集（或包含关系）的其它会话；点击打开新窗体（独立加载）。
- Tab5 人物传记：仅在 1v1（player:<id> 与 pawn:<id>）时开放；格式参考“前情提要”；可编辑。

## 6. 运行流程（最小实现）
1) 记录：编排完成后产出最终文本 → RecordEntryAsync（仅最终输出）。
2) 计数与总结：IRecapService 监听 OnEntryRecorded；每 N 轮做一次非流式总结；每 10 轮叠加到字典（长度受限）。
3) 提示组装：会话开始时，IPromptAssemblyService 收集固定提示词/传记/前情提要/选取历史片段，裁剪后注入 system 提示（策略侧调用）。
4) Embedding：仅对上述文本在运行期懒索引，不入档；更换配置或存档后可重建。

## 7. 持久化策略
- 历史、前情提要、固定提示词、人物传记：全部文本化入档。
- Embedding/RAG：仅运行期构建与缓存，严禁入档。
- 玩家 ID（player:<saveInstanceId>）与 agent:<guid> 首次生成并随档持久化。

## 8. DI 注册与依赖
- IParticipantIdService → ParticipantIdService
- IRecapService → RecapService（内部依赖 ILLMService、IHistoryService、ISchedulerService）
- IPromptAssemblyService → PromptAssemblyService
- 现有 HistoryService 扩展：子集检索、编辑 API、倒排索引增强。

## 9. 验收标准（Gate）
1) 历史仅含“最终输出”，不含中间过程；UI 可加载/分页/编辑并持久化。
2) 每 N 轮自动生成总结；每 10 轮叠加至前情提要字典（长度限制生效）。
3) 开启 1v1 时可查看/编辑人物传记；固定提示词可按参与者管理。
4) 子集检索：任意参与者子集能调取相关历史并用于提示注入。
5) Embedding 不入档；更换档或配置后可懒重建，不影响加载性能（无明显抖动）。
6) 提示词组装总长度不超过 MaxPromptChars，并可在日志/Debug 面板预览裁剪信息。

## 10. 任务与里程碑
- M1：接口与骨架
  - 定义并注册 IParticipantIdService、IRecapService、IPromptAssemblyService
  - HistoryService 扩展只读/写入/编辑/子集检索骨架
- M2：总结与字典
  - RecapService：N 轮总结 + 10 轮叠加；后台异步与护栏
  - 设置面板新增 History 配置项；热生效
- M3：UI 最小实现（5 Tab）
  - 历史记录/前情提要/固定提示词/关联对话/人物传记 的只读→可编辑
  - 加载键（ID 集合）选择与回填
- M4：编排接线与观测
  - IPromptAssemblyService 注入策略（Classic/EmbeddingFirst）
  - Debug 面板显示提示组装摘要与裁剪信息

## 11. 回滚
- 关闭历史总结与前情提要（禁用 RecapService），保留“仅记录最终输出”。
- 策略端停止注入组装提示（回到 PersonaSystemPrompt + 即时上下文）。

## 12. 风险与缓解
- 成本/时延：总结与叠加放后台并设超时；失败降级为跳过该轮
- 漂移与积累：提供“一键重述”压缩前情提要；UI 可编辑与修正
- ID 稳定性：player:<saveInstanceId> 固定，persona:<name>#<rev> 可追踪版本；显示名缓存避免离场空白

> 本方案与 V4 架构一致：历史入档、Embedding 不入档；提示词组装在编排前注入，工具与 RAG 仍遵循现有策略链与防护。


