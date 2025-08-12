# RimAI.Core V5 架构文档

> 版本：v5.0.0-alpha  
> 状态：Living Document – 每完成一个阶段（P1–P9）立即回填文档 & 图表

> 本文目标：
> 1. 在 V4 实践基础上，确立 V5 的全局纪律、职责边界与分层设计，保持“访问面最小、职责单一、强可观测”。
> 2. 指导 P1–P9 的施工推进，确保阶段实现不偏离整体架构，Gate 可录屏可复现。
> 3. 与《V5 — 全局纪律与统一规范》（`docs/V5_GLOBAL_CONVENTIONS.md`）协同，冲突时以“全局规范”为准。

---

## 1. 战略目标（Why V5?）

| 目标 | 说明 | 衡量指标 |
|------|------|----------|
| 访问面最小 | Verse/Framework 入口极度收敛，降低耦合与副作用 | 通过 Cursor 内置工具 Gate 检查（见 §13） |
| 施工可验证 | 每阶段有 Debug 面板与统一日志，Gate 可录屏复现 | 每 P 有“验收 Gate + 回归脚本” |
| 非流式纪律 | 后台/服务路径一律非流式；流式仅用于 Debug/UI | 后台检查=0: `StreamResponseAsync\(` |
| 单一事实源 | Tool JSON 的唯一产出方在 Tool Service；历史仅记录“最终输出”；Stage 统一写 `agent:stage` | CI 脚本与日志审计 |
| 配置与快照 | 对外仅暴露 `CoreConfigSnapshot`（不可变）；热重载事件广播 | P1 Gate：Reload 生效 |

对比 V4 的关键变化：

- Stage 厚度进一步压薄为“仲裁与路由”，业务流程放入 Act；统一写入 `agent:stage` 专属日志线程。
- 工具向量索引与 Tool JSON 产出完全下沉到 Tool Service；编排层不做向量匹配、不自动降级。
- 历史域仅保存“最终输出”，引入“单调回合序号 + 水位 + 幂等键”避免重复摘要。
- Verse 与 Scribe 的访问点固定在 P3/P6；其余模块禁用。

---

## 2. 与 V4 的关系

| 分类 | V4 形态 | V5 决策 |
|------|---------|---------|
| Stage | 较厚，含轮次与事件聚合 | 压薄为仲裁/路由；Act 插件化；统一历史汇聚 `agent:stage` |
| Tooling | 工具系统 + 可选向量匹配 | 工具索引完全内置，产出 Classic/TopK Tool JSON，无自动降级 |
| Orchestration | 五步工作流 + 策略切换 | 最小编排：Classic/TopK 二选一，一次非流式决策 + 串行执行 |
| History | 记录较多过程 | 仅“最终输出”；前情提要 Replace/Append；关联对话 |
| Persona | 依 UI 组织注入 | 独立“数据与素材域”，非流式生成草案，模板热重载 |
| Contracts | Core.Contracts 较多接口 | 对外最小面：仅 `IConfigurationService` + `CoreConfigSnapshot` |

---

## 3. 分阶段路线图（P1–P9）

> 细则与按钮清单见各 `V5_Px_IMPLEMENTATION_PLAN.md`。此处仅列阶段 Gate 摘要。

| 阶段 | 关键 Gate（必须通过） |
|------|-----------------------|
| P1 – Skeleton + DI & Config | Boot 横幅；Debug：Ping/ResolveAll/Config 预览 + Reload；Fail Fast & 环依赖检测 |
| P2 – LLM Gateway | 非流式回声/JSON/Embedding；流式 Demo；取消/超时/重试/断路；会话缓存失效 |
| P3 – Scheduler + WorldData | 主线程泵无卡顿；PingOnMainThread < 2ms；SpikeTest 清空队列；GetPlayerName 正确 |
| P4 – Tool System | Classic 返回全集 Tool JSON；索引自动构建/重建；TopK 查询 + 分数表；索引未就绪不降级 |
| P5 – Orchestration-Min | Classic/NarrowTopK 二选一；一次非流式决策 + 串行执行；Deep/Wide 返回未实现 |
| P6 – Persistence | SaveAll/LoadAll 节点统计；导出/导入 JSON；历史索引重建；全局唯一文件 IO 入口 |
| P7 – Persona | CRUD 与非流式生成草案；组合 Persona Block + 审计；键=实体 `pawn|thing:<loadId>` |
| P8 – History | 仅最终输出；N 轮摘要 Replace/Append；关联对话（严格超/子集）；编辑/删除→Stale/重建 |
| P9 – Stage | 仲裁（互斥/合流/冷却/幂等/lease）；Acts/Triggers 插件化；统一写 `agent:stage` |

---

## 4. 分层视图（V5）

```mermaid
graph TD
    UI["UI 层\n窗口/面板/Debug"] --> Stage
    UI --> Orchestration
    Orchestration --> Tooling
    Orchestration --> LLM["ILLMService (P2)"]
    Stage --> History
    Tooling --> LLM
    World["WorldData (P3)"] --> Scheduler
    Tooling --> World
    Stage --> World
    Persistence
    subgraph Infrastructure
        Scheduler
        Config[IConfigurationService & CoreConfigSnapshot (P1)]
        DI[ServiceContainer (P1)]
        Persistence[Persistence (P6)]
    end
```

> 访问边界：
> - Framework 仅在 `LLMService`（P2）。
> - Verse 读取仅在 `WorldDataService`（P3）与 `PersistenceService`（P6）。
> - Tool JSON 的唯一产出方：`IToolRegistryService`（P4）。

---

## 4.5 Core Contracts Layer（稳定对外层）

V5 对外最小面仅限配置：

| 分类 | 接口/DTO | 说明 |
|------|----------|------|
| 服务接口 | `IConfigurationService` | 只读接口，暴露不可变快照与热重载事件 |
| DTO | `CoreConfigSnapshot` | 对外只读快照；内部扩展在 `CoreConfig` 不破坏向后兼容 |

> 其余接口（LLM/Tooling/Orchestration/Persistence/Persona/History/Stage/World/Scheduler）均为 Core 内部 API，不进入 `RimAI.Core.Contracts`。

---

## 5. 模块蓝图（按阶段）

### 5.1 P1 – DI/Config/Debug

职责：容器（构造注入/预热/环依赖检测/健康信息）、配置加载与热重载（对外快照），最小 Debug 面板三件套。  
边界：仅本阶段触达 Contracts；为后续服务提供依赖根。  
纪律：禁止属性注入；Fail Fast；快照不可变；热重载事件广播。

### 5.2 P2 – LLM Gateway

职责：统一 Chat/Embedding（非流式/流式）、可靠性（超时/重试/断路）、错误映射、日志节流与心跳。  
边界：唯一 `using RimAI.Framework.*` 的位置；上游仅经 `ILLMService` 调用。  
纪律：后台非流式；`ConversationId` 必填；首包重试与心跳超时；可选内部事件。

### 5.3 P3 – Scheduler + WorldData

职责：主线程泵（队列预算/长任务告警）、只读世界数据防腐层（POCO）。  
边界：唯一 Verse 读取点在 `WorldDataService`；其他模块通过接口获取只读快照。  
纪律：帧预算 + 最大任务数；Verse 访问必须经主线程调度。

### 5.4 P4 – Tool System

职责：工具注册/参数校验/执行沙箱；内置向量索引（构建/检索/状态）；产出 Classic（全集）/NarrowTopK（TopK+分数）Tool JSON。  
边界：Embedding 经 `ILLMService`；索引仅以设置文件（JSON）持久化，不入游戏存档。  
纪律：索引未就绪不自动降级；不得直接 `using RimAI.Framework.*`；TopK 调用记录分数摘要与指纹；索引文件读写必须经 `IPersistenceService` 的统一文件 IO API。

### 5.5 P5 – Orchestration（仅编排）

职责：在上游固定模式下（Classic/TopK）获取 Tool JSON → 一次非流式 LLM 决策 Tool Calls → 串行执行并返回结构化结果。  
边界：仅消费 Tool Service 的 Tool JSON；不做向量；不做自动降级。  
纪律：`ExecutionProfile=Fast` 可用；`Deep/Wide` 直接返回未实现；后台非流式。

### 5.6 P6 – Persistence

职责：发令员（GameComponent 拦截存读档）→ 总工程师（唯一 Scribe 层）→ 专家（领域快照）。  
边界：Scribe/Verse 仅在 Persistence 模块；其他模块导出/导入 POCO 快照。  
纪律：节点级容错与统计；读档缺失节点返回空状态；历史双索引重建；全局唯一持久化 IO（Scribe + 文件）由 Persistence 统一承接。

### 5.7 P7 – Persona

职责：以实体键维护人格素材（职务/传记/意识形态/固定提示）；非流式生成“草案→人工采纳”。  
边界：不依赖 Stage/Orchestration/Tooling；只读世界快照经 `IWorldDataService`。  
纪律：禁用流式与 Verse/Scribe；模板母版 + 用户覆盖（热重载）；组合 Persona Block 受预算与审计。

### 5.8 P8 – History

职责：仅记录“最终输出”（用户/AI）；N 轮触发摘要（Replace/Append）；关联对话（严格超/子集）。  
边界：显示名解析在渲染期绑定；不将 Verse/Scribe 扩散到历史域。  
纪律：单调回合序号 + 水位 + 幂等键；编辑/删除→Stale 标记与重建；后台非流式生成摘要。

### 5.9 P9 – Stage（薄仲裁与路由）

职责：注册/启停 Acts 与 Triggers；仲裁（互斥/合流/冷却/幂等/并发上限/lease）；路由执行；统一写 “Stage 专属日志线程”。  
边界：Stage/Kernel/Service 不触达 LLM/Verse；Act 内如需文本生成，一律经 `ILLMService` 非流式。  
纪律：`agent:stage` 作为稳定 convKey；HistorySink 唯一写入点；Debug 面板展示注册表/票据/最近记录。

---

## 6. 全局纪律（并入）

> 完整版见 `docs/V5_GLOBAL_CONVENTIONS.md`。以下为关键不变式（Invariants）：

- 对外最小合同：仅 `IConfigurationService` + `CoreConfigSnapshot`。  
- 访问边界：Framework 仅 P2；Verse 仅 P3/P6；Tool JSON 唯一产出在 P4。  
- 非流式纪律：后台/服务路径禁用流式；流式仅 Debug/UI。  
- 键与 ID：`participantId` 规范；`convKey = join('|', sort(participantIds))`；保留 `agent:stage`。  
- 配置与热重载：不可变快照、事件广播、订阅方自行生效与重建。  
- 日志与观测：统一前缀 `[RimAI.P{n}.*]`；Debug 面板 `Px_*` 分节标准化。

---

## 7. 生命周期与启动流程

1) 启动：`ServiceContainer` 注册与预热 → 打印横幅与健康信息。  
2) GameComponents：注册 `SchedulerGameComponent`（P3）与 `PersistenceManager`（P6）。  
3) 自动动作：进入地图或第 N Tick 触发工具索引构建（按配置），索引文件读写通过 Persistence 统一 IO。  
4) 运行期：配置热重载 → 事件广播；索引/模板变化 → 标记 Stale/重建。  
5) 存读档：P6 负责 SaveAll/LoadAll；读档后重建历史索引与水位；可选恢复工具索引快照。

---

## 8. 数据与标识规范

- `participantId`：`pawn:<loadId>` / `thing:<loadId>` / `player:<saveInstanceId>` / `persona:<name>#<rev>`；保留 `agent:stage`。  
- `convKey = join('|', sort(participantIds))`（顺序无关、全局唯一）。  
- Persona `entityId`：仅 `pawn|thing:<loadId>`；禁止使用 `convKey`。

---

## 9. 配置与热重载

- 源于 RimWorld ModSettings。内部 `CoreConfig`；对外 `CoreConfigSnapshot`（不可变）。  
- `IConfigurationService.OnConfigurationChanged` 广播；订阅者：
  - P2 应用默认超时/断路/重试参数；
  - P3 更新帧预算与最大任务数；
  - P4 Embedding 供应商变更 → 索引 Mark Stale + 后台重建；
  - P6 节点/策略；P7 模板热重载；P8 N 值/MaxChars；P9 合流/冷却/并发上限/TTL。

---

## 10. 可观测性与日志

- 统一日志前缀：V5 版本下，所有日志必须以 `[RimAI.Core]` 开头；建议叠加阶段标识形成层级前缀，如 `[RimAI.Core][P1]`、`[RimAI.Core][P4]`。关键字段包括 provider/model/convId-hash/latency/chunks/score 摘要等。  
- Debug 面板规范：每 P 至少包含 Ping/自检/示例/指标卡；日志按钮严格不落敏感正文。  
- 审计字段：字符裁剪、窗口区间、索引指纹、命中率、慢执行告警、节点统计等。

---

## 11. 性能与可靠性预算

- 启动预热 ≤ 200ms；常态每帧新增 ≤ 1ms（关闭 Debug）。  
- P2：指数退避/断路；首包重试与心跳超时。  
- P3：帧预算 + 最大任务数 + 长任务告警；可选优先级队列。  
- P4/P9：并发/速率上限、合流窗口、冷却时间、幂等 TTL 可调。  
- P6：中等规模存档保存 ≤ 150ms；读档 ≤ 200ms。

---

## 12. 安全与隐私（最小披露）

- 日志以散列 ID 表示参与者/会话，避免泄露原文；不输出敏感正文。  
- 历史仅保存最终输出与必要元信息；中间过程不入档。  
- 工具索引只以设置文件（JSON）形式持久化，不入游戏存档；路径与读写由 Persistence 统一管理。

---

## 13. 质量门禁（CI / 内置工具 Gate 摘要）

- Verse/Scribe 面最小化：除 `Modules/World/**` 与 `Modules/Persistence/**` 外 检查=0：`\bScribe\.|using\s+Verse`。  
- Framework 面最小化：除 `Modules/LLM/**` 外 检查=0：`using\s+RimAI\.Framework`。  
- 后台非流式：相关目录 检查=0：`StreamResponseAsync\(`。  
- Tooling 不自动降级：索引/TopK 路径 检查=0：`\bAuto\b|degrad|fallback`（上下文限定）。  
- 注入纪律：禁止属性注入；仅构造函数注入；Service Locator 禁用。  
- 文件命名与工件：构建后应存在 `tools_index_{provider}_{model}.json`（仅设置文件）；存档节点按版本后缀；仓级 检查禁用除 Persistence 外的 `System.IO` 直接使用。

> Gate 执行方式：统一由 Cursor 内置工具在提交前/PR 审阅时运行，不再依赖外部脚本。

---

## 14. 风险与缓解

| 风险 | 应对策略 |
|------|----------|
| 构建/触发风暴 | 合流 + 冷却 + 幂等；并发/速率上限；必要时全局节流 |
| 长任务阻塞 | 分帧与后台预处理；主线程仅执行最小段；预算 warn |
| 指纹失配/索引缺失 | 标记 Stale + 后台重建；面板可一键重建/导入导出 |
| 成本与速率限制 | 依赖 Framework 的缓存/合流；短上下文与预算控制 |
| 存档体积膨胀 | 仅“最终输出”入档；工具索引不入档，始终为设置文件；记录体积估算 |

---

## 15. 版本与提交流程

1. 分支策略：`feature/V5-Px_*`；合并 `dev` 触发 CI。  
2. PR 规范：引用修改的 `V5_Px_IMPLEMENTATION_PLAN.md` 小节与本文件章节；附 Debug 录屏。  
3. Tag 命名：`core/v5.{阶段序号}.{patch}`（如 `core/v5.4.1`）。

---

> 本文自 v5.0.0-alpha 起生效。任何修改请在 PR 描述写明「更新 ARCHITECTURE_V5.md 第 X 节」。


---

## 16. 异步与线程模型指引（按 P）

> 目的：避免网络/流式/文件 IO/重计算等长耗时操作阻塞游戏主线程，引发卡顿。以下列出各 P 的异步“必须/建议”与主线程守则。

- P1 DI/Config/Debug（非必须异步）
  - 典型点：容器预热、ResolveAll、配置重载。
  - 要求：操作应短小（<100ms）；Debug 按钮不做长任务；如有重扫描，使用后台 Task。

- P2 LLM Gateway（必须异步）
  - 典型点：`GetResponseAsync`、`StreamResponseAsync(IAsyncEnumerable<...>)`、`GetEmbeddingsAsync`。
  - 要求：全部返回 Task/可取消；默认超时；流式含心跳；禁止在主线程同步等待。

- P3 Scheduler/WorldData（必须异步 + 主线程化）
  - 典型点：`ScheduleOnMainThreadAsync`/`DelayOnMainThreadAsync`/`SchedulePeriodic`。
  - 要求：所有 Verse 访问必须经 `ScheduleOnMainThread*`；调用方以 Task 方式等待；禁止主线程 `.Wait()`/`.Result`。

- P4 Tooling（必须异步）
  - 典型点：索引构建/重建、`GetNarrowTopKToolCallSchemaAsync`、Embedding 批量、索引文件读写（经 Persistence）。
  - 要求：构建/重建在后台任务执行；文件 IO 使用 `IPersistenceService` 的异步 API；TopK 查询使用 Task 防止 UI 卡顿。

- P5 Orchestration（必须异步）
  - 典型点：`IOrchestrationService.ExecuteAsync`、逐条 `ExecuteToolAsync`。
  - 要求：一次非流式 LLM 决策 + 串行执行均为异步；全链支持取消与超时；禁止阻塞主线程。

- P6 Persistence（必须异步：文件 IO；Scribe 例外）
  - 典型点：导出/导入 JSON、工具索引文件读写（均经 Persistence）。
  - 要求：文件 IO 使用异步 API；`GameComponent.ExposeData()` 的 Scribe 调用受 RimWorld 约束为同步，但应保持最小工作量；Debug 面板的导入/导出走后台任务。

- P7 Persona（必须异步）
  - 典型点：`GenerateDescriptionFromNameAsync`、`GenerateDraftAsync`、`GenerateAsync`、模板热重载（文件 IO 经 Persistence）。
  - 要求：所有生成均为非流式异步；必要的世界采样经 P3 主线程化；模板文件读取使用异步文件 API。

- P8 History（必须异步）
  - 典型点：`AppendPairAsync/AppendAiFinalAsync`、分页 `GetThreadAsync`、`IRecapService.EnqueueGenerateIfDueAsync/ForceRebuildAsync`、显示名解析。
  - 要求：写入与生成在后台任务执行；Recap 生成可取消并限时；显示名解析若触达世界数据需经 P3。

- P9 Stage（必须异步）
  - 典型点：`IStageService.SubmitIntentAsync/StartAsync`、`IStageAct.ExecuteAsync`、`IStageTrigger.RunOnceAsync`、`StageHistorySink` 写入。
  - 要求：仲裁/路由/执行全链异步；Act 内如需世界快照经 P3；最终总结写入历史使用异步；禁止在主线程等待外部 Task。

主线程守则（通用）：
- 在 `Update/Tick`/UI 线程严禁调用阻塞等待（`.Wait()`/`.Result`/长时间锁）。
- 任何 Verse 访问必须通过 `ISchedulerService` 主线程化；任何文件 IO 必须通过 `IPersistenceService` 异步 API。


