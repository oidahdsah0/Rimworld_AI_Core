# P11 实施计划：舞台服务（Stage Service）

> 目标：在 Persona 仅负责“提示组装 + 按模式请求”的前提下，新增“舞台服务（Stage Service）”用于上游协调：会话归一化、并发/幂等、轮次驱动与历史落盘。Chat UI 作为一种舞台服务的实现，仅在 UI 内部使用流式，其余后台/服务固定非流式。

---

## 1. 背景与定位

- Persona 会话服务（`IPersonaConversationService`）已明确职责：只组装 system 提示并按流式/非流式返回，不写历史。
- “舞台服务”位于 UI/后台业务 与 Persona 之间，统一处理会话键、合流、冷却、轮次调度与历史写入，保证“仅记录最终输出”。
- 纠正规约：
  - 所有“非玩家发起”的对话，必须经由“舞台服务”处理；
  - 参与者列表最少包含 2 个单位（MinParticipants=2），不足则直接拒绝或延迟聚合；
  - 舞台服务负责校验参与者适配性（适合进入对话），允许“小人（Pawn）”与“AI 服务器（Server）”作为对话发起者/参与者。
- 使用场景：
  - 两台 AI 服务器同时触发同一会话（同一参与者集合） → 舞台服务调度轮次，逐个调用人格非流式，写入历史。
  - 多个小人（≥3）发起群聊 → 舞台服务调度轮次，逐个调用人格非流式，写入历史。

---

## 2. 概念与职责

- 会话键 convKey：对参与者 ID 集合排序后 `join('|', ids)`，与顺序无关，保证全局唯一（示例：`pawn:A|pawn:B|pawn:C`）。
- 并发治理：
  - 会话锁：`lock(convKey)` 串行化同一会话执行，避免交错与重复写入。
  - 合流窗口 CoalesceWindowMs（默认 300ms）：窗口内同 convKey 的多源触发合并一次执行。
  - 幂等键 idempotencyKey：`hash(sourceId + convKey + scenario + seed)`，重复触发复用进行中的结果或缓存结果。
  - 冷却 CooldownSeconds：同一 convKey 完成后一定时间内忽略重复触发，防骚扰。
- 舞台服务职责：
  - 决定调用模式（Chat UI 固定流式，后台固定非流式）；
  - 参与者集合归一化与 convKey 生成；
  - 历史查询/初始化与最终输出写入；
  - 轮次调度、超时/重试、事件广播与审计。
- Persona 职责：
  - 基于 `IPromptAssemblyService` 组装提示；
  - 按模式请求 `ILLMService`（后台固定非流式），返回文本；
  - 不处理历史，不管理状态。

### 2.1 参与者与触发来源校验（Eligibility）

- 最小参与者数量：`MinParticipants = 2`，小于 2 直接拒绝（返回原因 `TooFewParticipants`）。
- 触发来源：`Origin` 可为 `PlayerUI | PawnBehavior | AIServer | EventAggregator | Other`。
  - 非玩家发起（`Origin != PlayerUI`）必须经由舞台服务；玩家发起（Chat UI）可视为一种舞台服务实现。
- 参与者适配性（示例规则，可配置）：
  - 小人（Pawn）：非敌对、非战斗中、非失能/疯狂/睡眠、距离条件满足、在线可交互；
  - 服务器（Server）：在线、健康状态良好、未达并发上限；
  - 自检失败的参与者将被剔除；若剔除后参与者 < MinParticipants → 拒绝。
- 超限保护：`MaxParticipants`（默认 5，可在设置中调整，最大 10），超过则裁剪为前 K 名（按距离/优先级）。

### 2.2 表演项（Stage Acts）抽象与角色

- 定义：舞台服务承载的“表演项”（Act）是一类可插拔的对话/互动剧目，拥有独立的参与者约束、轮转规则、场景提示与终止条件。
- 例子（可扩展）：
  - GroupChat（群聊，当前已定义的 Act）；
  - Interrogation（审讯）；
  - Trial（法庭审判）；
  - Negotiation（谈判）；
  - Diplomacy（外交）；
  - Custom（Mod 扩展）。
- 角色（Roles）：Act 可声明参与者角色与数量（如审讯需要 Interrogator/Suspect、审判需要 Judge/Prosecutor/Defendant/Witness），舞台服务负责分配或映射实际参与者至角色。
- 终止条件：按 Act 自身定义（轮次、裁决产出、满意度阈值、超时等）。
- 提示策略：Act 可复用全局 Topic/Scenario 生成，也可覆盖为 Act 专属的开场白与约束提示。

---

## 3. 典型流程

### 3.1 两台 AI 服务器同时触发同一会话

0) 入口校验：
   - `Origin = AIServer`（非玩家）→ 必须走舞台服务；
   - 过滤参与者并确保 `Count >= 2`，否则拒绝；
1) 收到触发（包含 `sourceId`、`participants[]`、`userInput/scenario`、`priority?`、`idempotencyKey?`、`origin`）。
2) 生成 `convKey`，按 convKey 进入合流窗口（CoalesceWindowMs）收集同类触发。
3) 进入会话锁，挑选“主触发”（按 priority/sourceId 稳定规则）。
4) 历史查找：若存在则复用，否则 `CreateConversation(participants)` 初始化。
5) 选题与开场白（Topic + Scenario）：基于 `convKey+seed` 选择 Topic（可复现、去重），生成简短开场白并以会话级 scenario 覆盖注入：`IFixedPromptService.UpsertConvKeyOverride(convKey, scenarioText)`；
6) 调用 Persona（非流式）：`BuildSystemPromptAsync(participants, Chat, userInput=turnInstruction)`；
7) 返回完整文本后，舞台服务写历史（仅最终输出），并广播审计事件（含合流元数据与 TopicSelected）；
7) 释放锁；窗口内其他触发复用本次结果。

### 3.2 三个小人的群聊（示例 K=3）

0) 扫描（每 N 秒）：发现周围 K 个非敌对角色 → 以概率 P 触发群聊。
1) 入口校验：`Origin = PawnBehavior`（非玩家）→ 必须走舞台服务；过滤后确保参与者 `Count >= 2`，否则不触发。
2) 组建参与者列表与 `convKey`；选题并生成开场白 scenario，调用 `FixedPromptService.UpsertConvKeyOverride(convKey, scenarioText)` 注入“场景提示”（会话级，整场复用）；
3) 进入会话锁，按 `seed = hash(convKey + gameTick)` 稳定随机化发言顺序，设定轮次 T（玩家可配置）。
4) 历史查找/初始化；
5) For 轮次 i=1..T：
   - 取当前发言者 `speakerId`；构造简短指令 `turnInstruction`（如“轮到{昵称}发言：{主题/上下文摘要}”）；
   - Persona 非流式调用 → 获得完整文本；
   - 舞台服务以“气泡”展示，并写历史（仅最终输出：`speakerId` 与文本）；
   - 检查终止条件（轮次达标/超时/异常/参与者离开）→ 结束或继续；
6) 清理会话级场景提示覆盖（恢复/删除 `convKey` override）。

---

## 4. 与提示/历史/事件的集成

- 提示注入：优先使用 `fixed_prompts` 段的 convKey 覆盖作为“场景提示”（简洁、可复用）；必要时可后续增加模板段 `scenario`。场景文本由组织者选题后生成，建议 300–600 字，包含“主题/参与者/背景/规则/开场白建议”。
- 历史策略：仅写“最终输出”（用户/AI/工具）；群聊可在历史元数据中记录 `audience=[...]` 与 `coalesced`、`seed` 等信息。
-- 事件与审计：
  - 广播 `OrchestrationProgressEvent`：`StageStarted` → `Coalesced` → `TurnCompleted`(多次) → `Finished`；
  - 透传 `PromptAssemblyService` 的 `PromptAudit` 摘要（段落注入与截断信息）。
  - 新增 `TopicSelected` 事件（payload：convKey、topic、seed、sourceWeights、scenarioChars）。

---

## 5. 配置项（建议新增 `CoreConfig.Stage`）

- `CoalesceWindowMs`（int，默认 300）：合流窗口时长。
- `CooldownSeconds`（double，默认 30）：会话冷却时间。
- `GroupChatProbabilityP`（double，默认 0.2）：扫描命中后触发群聊的概率。
- `GroupChatMaxRounds`（int，默认 2）：群聊总轮次上限（或“每人 N 轮”策略的参数）。
- `MaxLatencyMsPerTurn`（int，默认 5000）：单轮最大等待时延。
- `RetryPolicy`：`MaxAttempts`（默认 1）/`BackoffMs`（默认 800）。
- `LocaleOverride`（string，可空）：覆盖语言；为空则使用模板服务的默认解析。
- `MinParticipants`（int，默认 2）：最小参与者数；不满足则拒绝。
- `MaxParticipants`（int，默认 5，最大 10）：最大参与者数；超过则裁剪。
- `PermittedOrigins`（集合）：允许的非玩家触发来源枚举。
- `EligibilityRules`：启用/禁用具体校验开关（如“禁止睡眠中参与”、“禁止战斗中参与”等）。

### 5.1 扫描相关配置（Stage.Scan）

- `Scan.IntervalSeconds`（int，默认 300）：舞台服务扫描周期。
- `Scan.MaxNewConversationsPerScan`（int，默认 2）：单次扫描最多新建会话数，防止风暴。
- `Scan.Enabled`（bool，默认 true）：是否启用扫描。

### 5.2 邻近度扫描配置（Stage.ProximityScan）

- `ProximityScan.Enabled`（bool，默认 true）：启用邻近度扫描项。
- `ProximityScan.RangeK`（float，默认按游戏尺度设定）：邻近判定距离阈值。
- `ProximityScan.TriggerMode`（enum，`Threshold|Probability`，默认 `Threshold`）：触发判定模式。
- `ProximityScan.TriggerThreshold`（double，默认 0.5）：阈值模式下，随机数 r 超过该值即触发；设为 1 可关闭群聊。
- `ProximityScan.ProbabilityP`（double，默认 0.0）：概率模式下，r < P 触发（当 `TriggerMode=Probability` 生效）。
- `ProximityScan.FactionFilter`（enum/flags，默认允许非敌对阵营）：参与者阵营过滤。
- `ProximityScan.OnlyNonHostile`（bool，默认 true）：仅限非敌对。
- `ProximityScan.ExcludeBusy`（bool，默认 true）：排除已在会话/任务中的单位。

### 5.3 选题与开场白（Stage.Topic）

- `Topic.Enabled`（bool，默认 true）：开启选题与开场白生成。
- `Topic.Sources`（权重字典，可扩展提供器）：`{HistoryRecap:0.4, WorldContext:0.3, Relations:0.2, RandomPool:0.1}`。
- `Topic.MaxChars`（int，默认 600）：场景文本长度上限。
- `Topic.DedupWindow`（int，默认 5）：避免与最近 N 次相同 convKey 的话题重复。
- `Topic.SeedPolicy`（enum，`ConvKeyOnly|ConvKeyWithTick`，默认 `ConvKeyWithTick`）。
- `Topic.Locale`（可空）：为空沿用模板解析语言。

> 说明：`Topic.Sources` 的键名对齐“选题提供器”的 `Name`，权重用于从多提供器中按比例随机挑选。可通过新增实现类扩展来源，无需更改核心代码。

> UI：设置页增加“对话组织者”分节，允许玩家调整轮次、概率 P、冷却与合流窗口。

---

## 6. 接口草案（不落代码，仅约定）

- `IStageService`
  - `StartAsync(StageRequest request)`：按 `request.Stream=false/true` 返回非流式/流式（UI 仅用流式展示；后台固定非流式）。
  - `StageRequest`：
    - `participants[]`（必填，进入前会做 Eligibility 过滤）；
    - `mode: Chat|Command`，`stream`；
    - `origin: ConversationOrigin`（`PlayerUI|PawnBehavior|AIServer|EventAggregator|Other`）；
    - `initiatorId`（触发者 ID，可为 pawn/server）；
    - `userInput/scenario`，`sourceId`，`idempotencyKey?`，`priority?`，`seed?`，`locale?`，`targetParticipantId?`。
  - `StageOptions`：`CoalesceWindowMs`, `CooldownSeconds`, `MaxRounds`, `RetryPolicy`, `LocaleOverride`。
  - 内部状态：`StageConversationState`（`convKey`, `participants[]`, `seed`, `totalRounds`, `currentIndex`, `startedAt`, `sourceIds[]`, `coalesced`, `scenarioOverrideSnapshot`）。

- 扫描接口（可选抽象）：
  - `IStageScan`
    - `Task<IReadOnlyList<ConversationSuggestion>> RunAsync(ScanContext ctx, CancellationToken ct)`
    - `ConversationSuggestion`：`participants[]`, `origin`, `initiatorId`, `scenario?`, `priority?`, `seed?`
  - 舞台服务提供 `RunScanOnceAsync()`，内部顺序执行已注册扫描项，汇总候选后做去重/冷却/合流并触发会话。

### 6.1 选题管线（可扩展的 Topic Providers）

- `ITopicProvider`
  - `string Name { get; }`（与 `Topic.Sources` 的键名对齐）
  - `Task<TopicResult> GetTopicAsync(TopicContext ctx, CancellationToken ct)`
  - `TopicResult`：`Topic`（简短标题）/`ScenarioText`（300–600 字开场白）/`Source`（Name）/`Score?`（可选）
- `TopicContext`
  - `convKey`, `participants[]`, `seed`, `locale`, `recentRecaps`, `recentHistory`, `worldSnapshot`, `relationsSnapshot`
- `ITopicService`（或 `TopicSelector`）
  - `Task<TopicResult> SelectAsync(TopicContext ctx, IReadOnlyDictionary<string,double> weights, CancellationToken ct)`
  - 逻辑：按 `weights` 在已注册 `ITopicProvider` 中加权随机选一到多提供器尝试；支持回退链；对 `Topic.DedupWindow` 做去重；裁剪到 `Topic.MaxChars`；发布 `TopicSelected` 事件。
- 注册与扩展
  - 通过 DI 自动注册所有实现 `ITopicProvider` 的类型（类似工具注册/扫描项注册）。
  - 默认内置至少两个提供器：`HistoryRecapProvider` 与 `RandomPoolProvider`；可后续新增 `WorldContextProvider` / `RelationsProvider`。

### 6.2 表演项接口（Stage Acts）

- `IStageAct`
  - `string Name { get; }`（如 GroupChat/Interrogation/Trial/Negotiation/Diplomacy/Custom）
  - `bool IsEligible(ActContext ctx)`（是否可执行：校验参与者数量/角色匹配/场地要求等）
  - `Task<ActResult> RunAsync(ActContext ctx, CancellationToken ct)`（执行该 Act：驱动轮次、调用 Persona、写历史、广播事件）
- `ActContext`
  - `convKey`, `participants[]`, `roles?`, `seed`, `locale`, `options`, `services`（用于访问 Persona/History/Topic 等服务）
- `ActResult`
  - `Completed`，`Reason`（如 MaxRoundsReached/Timeout/Aborted），`Metrics`（轮次/耗时等）
- 扩展与选择
  - 由舞台服务注册多个 `IStageAct`；通过配置或扫描建议决定本次使用何种 Act（默认 `GroupChat`）。

---

## 7. 质量与异常处理

- 并发一致性：按 convKey 加锁 + 合流窗口；幂等缓存短期保存最近结果。
- 失败策略：单轮失败重试一次；仍失败则跳过该发言者或提前结束（按配置）。
- 超时保护：超时视为失败；写审计事件，不污染正文历史。
- 性能预算：群聊每轮调用控制在 `MaxLatencyMsPerTurn` 内；总帧耗时≤1ms（主线程只做调度与 UI 刷新）。

---

## 8. 里程碑与 Gate

- M1：骨架与配置
  - 定义 `IStageService` 契约与最小实现骨架；
  - convKey 归一化、会话锁、合流窗口与幂等缓存；
  - Eligibility：最小参与者=2、触发来源校验、参与者适配性过滤；
  - 配置项 `CoreConfig.Stage` 与设置页（读取生效即可）。
  - Gate：
    - 非玩家发起请求仅能通过组织者入口；
    - 参与者<2 时拒绝；
    - 同 convKey 并发触发只执行一次；合流窗口内触发被合并；冷却生效。

- M2：Persona 集成 + 历史写入
  - 后台非流式调用 Persona，完整文本返回；
  - 历史仅写“最终输出”，包含最小元数据；
  - 广播 Stage 进度事件；
  - Gate：两台服务器并发触发场景通过录屏验证（单次输出、单次历史）。

- M3：群聊轮次调度
  - 稳定随机顺序与轮次推进；
  - 组织者选题并生成开场白 scenario；以 convKey 覆盖注入，结束后清理；
  - Topic 管线可扩展：至少实现 `HistoryRecapProvider` 与 `RandomPoolProvider`，按 `Topic.Sources` 权重选择；
  - Gate：3 小人群聊 N 轮跑通，历史写入符合规范且可回放。

- M4：扫描管线与邻近度扫描
  - 实现 Organizer 定时扫描（默认 300s）；
  - 实现 `ProximityScan`：K 距离邻近统计、随机落点、小人对匹配、阈值/概率触发；
  - 支持单次扫描新建会话上限与冷却；
  - 事件：`StageScanStarted`/`ConversationSuggested`/`ConversationTriggered`/`ScanCompleted`；
  - Gate：
    - 扫描能在 Demo 地图稳定触发 2 人群聊；
    - 设置里禁用或将阈值设为 1 时不触发群聊；
    - 达到当轮上限后不再新建会话；
    - 性能预算与主线程流畅度满足要求。

- M4：Debug/观测与回归
  - Debug 面板新增“触发群聊”与“触发双源并发”按钮；
  - Prompt 审计与 Organizer 事件在 UI 可见；
  - Gate：回归用例全绿，性能预算符合要求。

---

## 9. 风险与缓解

- 触发风暴：通过 Cooldown 与合流窗口抑制；必要时引入全局节流。
- 上下文过长：`History.MaxPromptChars` 与模板裁剪已覆盖；必要时降低段落包含项。
- 行为不可预期：事件/审计全链路可观测，便于快速回滚与调参。

---

## 10. 扫描调度与扫描项（Scan Pipeline）

### 10.1 扫描调度

- 组织者每 `Scan.IntervalSeconds`（默认 300s）执行一次扫描任务（可停可配）。
- 扫描为可扩展管线：按注册顺序执行多个扫描项，聚合产生 `ConversationSuggestion` 列表，随后统一去重/冷却/合流→触发。
- 并发保护：扫描器本身串行；触发会话按 `convKey` 持锁；同一 `convKey` 在 300ms 合流窗口内合并为一次。

### 10.2 扫描项一：邻近度扫描（ProximityScan）

参数：
- 距离阈值 `RangeK`；
- 触发模式 `TriggerMode=Threshold|Probability`；
- `TriggerThreshold`（阈值模式）或 `ProbabilityP`（概率模式）；
- `MaxNewConversationsPerScan`；阵营过滤与 Eligibility 规则。

步骤：
1) 遍历符合 Eligibility 的可对话小人，统计其 K 距离内的符合条件角色数量 P；
2) 收集 P>0 的小人集合，随机“落点”到其中一个小人 A；
3) 从 A 的近邻中随机挑选 B，构成二人候选对（确保 `MinParticipants=2`）；
4) 生成 `convKey=sort(A,B)`，检查冷却/幂等/合流；
5) 触发判定：
   - 阈值模式：r=NextDouble()，若 r>TriggerThreshold 则触发（TriggerThreshold=1 表示关闭）；
   - 概率模式：r=NextDouble()，若 r<ProbabilityP 则触发；
6) 通过后：
   - 以 `Origin=PawnBehavior` 生成建议项（participants=[A,B]，initiatorId=A，scenario 可选）；
   - Organizer 执行非流式会话：初始化/复用历史、注入（可选）场景提示、按轮次推进并写入最终输出；
7) 达到 `MaxNewConversationsPerScan` 时提前结束本轮扫描。

事件：
- `OrganizerScanStarted`（payload：tick/地图/参数）
- `ConversationSuggested`（payload：convKey、participants、origin、seed、r、mode）
- `ConversationTriggered`（payload：convKey、participants、origin、触发模式与命中与否、冷却/合流状态）
- `ScanCompleted`（payload：suggestions,totalTriggered,elapsedMs）

性能与安全：
- 扫描可分帧执行；达到上限早停；
- 参与者忙碌或会话中排除；
- 失败/异常仅记录审计不过度打扰；
- 通过配置可完全关闭群聊触发（如 `TriggerMode=Threshold` 且 `TriggerThreshold=1`）。

---

## 11. 与现有模块的关系

- 不变更 Persona 接口与职责；
- 与 `IHistoryWriteService/IHistoryQueryService` 对齐，仅在组织者处写入最终输出；
- 复用 `IPromptAssemblyService` 与模板机制，无需改动；
- Chat UI 继续独立流式路径，不受影响；后台统一经组织者走非流式路径。

> 合并要求：提交 PR 前需附录屏，验证 M1–M3 Gate；文档同步更新本文件完成度标记。


