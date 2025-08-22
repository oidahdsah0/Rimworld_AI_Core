> 最新注意事项（Debug 页面移除）
>
> - Debug 页面（DebugPanel/Debug Window）已从项目中移除，后续不再需要。
> - 本文中涉及 Debug 面板/按钮/日志/流式相关内容保留仅作历史参考，不再作为必须项或 Gate 依赖。
> - 与流式相关的约束更新：仅 ChatWindow UI 允许流式展示；后台/服务路径一律非流式。原文中“UI/Debug”的表述请等价理解为“仅 ChatWindow UI”。
> - Gate 更新：关于流式 API 的允许路径应视为仅 `Source/UI/ChatWindow/**`；`Source/UI/DebugPanel/**` 不再适用。

# RimAI.Core V5 架构文档

> 版本：v5.0.0-alpha  
> 状态：Living Document – 每完成一个阶段（P1–P13）立即回填文档 & 图表

> 本文目标：
> 1. 在 V4 实践基础上，确立 V5 的全局纪律、职责边界与分层设计，保持“访问面最小、职责单一、强可观测”。
> 2. 指导 P1–P13 的施工推进，确保阶段实现不偏离整体架构，Gate 可录屏可复现。
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

## 3. 分阶段路线图（P1–P13）

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
| P10 – ChatWindow UI | 闲聊真流式（仅 UI/Debug）；命令伪流式；仅“最终输出”写入历史；Gizmo 入口与快捷键 |
| P11 – Prompting | 单入口 `IPromptService.BuildAsync`；12 项 ChatUI 作曲器；多语言 JSON；热插拔；全链非流式 |
| P12 – ChatUI 指令模式 | 编排（非流式）→ RAG 合并 → UI 真流式；PlanTrace 写历史（AI Note, TurnOrdinal=null）；工具 DisplayName；Prompt 支持 ExternalBlocks |
| P13 – Server 服务 | Server 基础信息/巡检/提示词；`Servers` 快照节点；槽位与定时执行；温度→采样温度映射；与 P11 外部块对接 |

---

## 4. 分层视图（V5）

```mermaid
graph TD
    UI["UI 层\n窗口/面板/Debug"] --> Stage
    UI --> Orchestration
    UI --> Prompting["Prompting (P11)"]
    Orchestration --> Tooling
    Orchestration --> LLM["ILLMService (P2)"]
    Stage --> History
    Stage --> Prompting
    Tooling --> LLM
    World["WorldData (P3)"] --> Scheduler
    Tooling --> World
    Stage --> World
    Persistence
    Prompting --> Server["Server Service (P13)"]
    Server --> World
    Server --> Tooling
    Server --> LLM
    Server --> Persistence
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
> - Prompting 单一入口：`IPromptService.BuildAsync`（P11）组织提示词与上下文；不触达 Framework；Verse/文件 IO 访问遵循 P3/P6。
> - Server（P13）：不直接触达 Verse；工具执行经 `IToolRegistryService`；世界数据仅经 `IWorldDataService`；文件/预设读取仅经 `IPersistenceService`；如需 LLM，仅经 `ILLMService`（后台非流式）。

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

### 5.10 P10 – ChatWindow（信息传输 UI）

职责：独立聊天窗口（玩家 ↔ 小人），闲聊真流式（UI 展示）、命令后台非流式 + UI 伪流式；指示灯（Data/Busy/Fin）、LCD 跑马灯与“生命体征”小窗；仅在完成阶段将“玩家输入 + AI 最终文本”写入历史。  
边界：UI 不直接 `using RimAI.Framework.*`；世界数据经 `IWorldDataService`（P3，主线程化）；历史写入经 `IHistoryService`（P8）；可选调用 `IOrchestrationService`（P5）执行命令；提示词统一由 `IPromptService`（P11）提供；不触达 Scribe/文件 IO。  
纪律：流式仅限 UI/Debug；后台路径一律非流式；取消与会话自增编号避免尾包污染；日志前缀 `[RimAI.Core][P10]`；入口为 Pawn 常规 Gizmo“信息传输”。

---

### 5.11 P11 – Prompting（提示词服务）

职责：单入口 `IPromptService.BuildAsync`，以可插拔作曲器（Composer）组织 SystemPrompt、ContextBlocks 与用户输入前缀；支持多语言 JSON、配置热插拔与预算裁剪；为 ChatUI/Stage/Tool 提供统一提示词产物。  
边界：不触达 LLM/Framework；Verse 仅经 `IWorldDataService`（P3）获取只读快照；文件/本地化 JSON 仅经 `IPersistenceService`（P6）；可消费 P7/P8 快照（Persona/历史）。  
纪律：全链非流式；作曲器按 `Scope/Order` 装配；缺键/缺数据容错跳过；日志前缀 `[RimAI.Core][P11]`；返回纯 POCO/字符串，不泄露 Verse 句柄。

---

### 5.12 P12 – ChatUI 指令模式（编排→RAG→流式）

职责：命令请求两段式闭环：段1 编排（非流式：获取工具 JSON→一次决策→串行执行→产出结构化结果与 PlanTrace）；段2 UI 真流式（将工具结果作为 RAG 上下文合入 Prompt 后发起流式回答）。
边界：编排不触达 Framework/Verse，仅经 P2/P3 规定入口；PlanTrace 通过 `IHistoryService.AppendAiNoteAsync` 写入历史但不推进回合；Prompting 支持 `ExternalBlocks` 合并工具结果。
纪律：后台非流式；仅 ChatWindow 真流式；`IRimAITool.DisplayName`（本地化短名）用于文案与 UI；日志前缀 `[RimAI.Core][P12]`。

---

### 5.13 P13 – Server 服务（基础信息/巡检/提示词）

职责：维护服务器基础信息（Lv1–Lv3）、人格槽位与巡检槽位；按计划串行执行已分配工具并汇总摘要；依据机房温度映射采样温度与提示词变体；为 Act/Chat 提供 `ServerPromptPack`（SystemLines/ContextBlocks/Temperature）。
边界：SSOT 在 ServerService；持久化通过 P6 的 `Servers` 节点；世界数据只读经 P3；工具执行经 P4；如需 LLM 经 P2（后台非流式）；提示词并入经 P11（`ExternalBlocks` 或 Composer）。
纪律：文件 IO 仅经 P6；槽位/工具等级与最小间隔（≥6h）校验；读档后错峰与重置 NextDue；日志前缀 `[RimAI.Core][P13]`。

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
   - 同步：发现通电服务器并注册巡检周期任务（P13）：`IServerService.StartAllSchedulers(appRootCt)`；最小间隔 6 小时；读档/导入后重置 `NextDue` 以错峰。
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
  - P12 ExternalBlocks/模板变更生效；P13 Server 预设与模板热重载（经 Persistence 设置文件）。

---

## 10. 可观测性与日志

- 统一日志前缀：V5 版本下，所有日志必须以 `[RimAI.Core]` 开头；建议叠加阶段标识形成层级前缀，如 `[RimAI.Core][P1]`、`[RimAI.Core][P4]`、`[RimAI.Core][P10]`、`[RimAI.Core][P11]`、`[RimAI.Core][P12]`、`[RimAI.Core][P13]`。关键字段包括 provider/model/convId-hash/latency/chunks/score 摘要等。  
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
- 后台非流式：相关目录 检查=0：`StreamResponseAsync\(`。例外：仅 `Source/UI/ChatWindow/**` 允许。  
- Tooling 不自动降级：索引/TopK 路径 检查=0：`\bAuto\b|degrad|fallback`（上下文限定）。  
- 注入纪律：禁止属性注入；仅构造函数注入；Service Locator 禁用。  
- 文件命名与工件：构建后应存在 `tools_index_{provider}_{model}.json`（仅设置文件）；存档节点按版本后缀；仓级 检查禁用除 Persistence 外的 `System.IO` 直接使用。
  - 指令两段式（P12）：编排与历史写入均为非流式；ChatUI 真流式仅在第二段；Orchestration 目录检查=0：`StreamResponseAsync\(`。
  - Server（P13）：所有文件 IO 经 Persistence；巡检最小间隔≥6h；工具分配强制 `tool.Level <= server.Level`（Level=4 工具在游戏内不可见）。
  - ChatUI 作曲器（Composer）纪律：
    - 标题、标签、标点、单位等一律经 `ctx.L/ctx.F` 取自本地化键（`prompt.section.*` / `prompt.label.*` / `prompt.punct.*` / `prompt.unit.*` / `prompt.token.*` / `prompt.format.*`）。
    - 禁用 `isZh`/语言分支与任何硬编码“总督/Governor/governor”称谓；称谓为空时必须按“目标语言→en”从 `ui.chat.player_title.value` 获取。
    - Composer 目录检查=0：`\bisZh\b|Governor|governor|总督|[：，；、]`；出现上述内容视为违反本地化纪律。

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
  - 典型点：`GenerateDraftAsync`、`GenerateAsync`、模板热重载（文件 IO 经 Persistence）。
  - 要求：所有生成均为非流式异步；必要的世界采样经 P3 主线程化；模板文件读取使用异步文件 API。

- P8 History（必须异步）
  - 典型点：`AppendPairAsync/AppendAiFinalAsync`、分页 `GetThreadAsync`、`IRecapService.EnqueueGenerateIfDueAsync/ForceRebuildAsync`、显示名解析。
  - 要求：写入与生成在后台任务执行；Recap 生成可取消并限时；显示名解析若触达世界数据需经 P3。

- P9 Stage（必须异步）
  - 典型点：`IStageService.SubmitIntentAsync/StartAsync`、`IStageAct.ExecuteAsync`、`IStageTrigger.RunOnceAsync`、`StageHistorySink` 写入。
  - 要求：仲裁/路由/执行全链异步；Act 内如需世界快照经 P3；最终总结写入历史使用异步；禁止在主线程等待外部 Task。

 - P10 ChatWindow（UI 允许流式；后台非流式）
  - 典型点：闲聊 `ILLMService.StreamResponseAsync`（UI 真流式展示）；命令经编排/服务非流式返回，UI 端伪流式切片；指示灯与 LCD 跑马灯逐帧刷新。
  - 要求：网络/服务调用均异步；使用 `CancellationTokenSource` 支持中断；OnGUI 每帧从 `ConcurrentQueue<string>` 消费 chunk 更新最后一条 AI 文本；禁止 `.Wait()`/`.Result` 阻塞主线程；流式 API 仅在 UI/Debug 路径。

 - P11 Prompting（必须异步；非流式）
  - 典型点：`IPromptService.BuildAsync` 聚合世界/人格/历史快照与多语言资源；执行作曲器流水线并裁剪预算。
  - 要求：全链非流式；Verse 访问主线程化经 P3；本地化 JSON 读取经 P6 的统一文件 IO；避免大字符串频繁拼接（建议 `StringBuilder`）。

 - P12 ChatUI 指令模式（两段式：后台非流式 + UI 真流式）
  - 典型点：`IOrchestrationService.ExecuteAsync`（后台非流式）→ ChatUI 将工具结果注为 `ExternalBlocks` 后调用 `ILLMService.StreamResponseAsync(UnifiedChatRequest)`（仅 UI）。
  - 要求：`AppendAiNoteAsync` 写入过程文案为异步且不推进回合；后台严禁使用流式；UI 流式期间支持取消与指示灯；禁止在主线程阻塞等待。

 - P13 Server（必须异步 + 周期调度）
  - 典型点：`StartAllSchedulers` 注册周期任务；`RunInspectionOnceAsync` 串行执行槽位工具；`BuildPromptAsync` 产出提示词块与采样温度。
  - 要求：周期任务在后台执行；如需 Verse 只读数据经 P3 主线程化；文件/预设读取使用 P6 异步 API；避免频繁大文本拼接与长任务阻塞。

 主线程守则（通用）：
- 在 `Update/Tick`/UI 线程严禁调用阻塞等待（`.Wait()`/`.Result`/长时间锁）。
- 任何 Verse 访问必须通过 `ISchedulerService` 主线程化；任何文件 IO 必须通过 `IPersistenceService` 异步 API。


