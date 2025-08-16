# RimAI V5 — 全局纪律与统一规范（Global Conventions）

> 本文为 V5 各阶段（P1–P9）的通用约束与术语规范，所有 Px 实施计划默认遵循本文件。若单个阶段存在特例，会在各自文档内明确“仅本阶段的补充”。

---

## 0. 适用范围

- 适用：`V5_P1..P9_IMPLEMENTATION_PLAN.md` 所述各子系统（DI/配置、LLM 网关、调度与世界数据、工具系统、仅编排、持久化、Persona、历史、Stage）。
- 不适用：旧版 V4 文档与实验性模块（已废弃或另有说明者除外）。

---

## 1. 全局不变式（Invariants）

- 合同最小面（Contracts）：
  - 对外稳定合同仅限 `IConfigurationService` 与 `CoreConfigSnapshot`（P1）。其余接口为 Core 内部 API，签名可按阶段演进但需后向兼容。

- 访问边界（单向依赖）：
  - LLM 与 Embedding 仅允许经 `ILLMService`（P2）。业务模块不得直接 `using RimAI.Framework.*`。
  - Verse 访问仅允许在 `WorldDataService`（P3，只读）与 `PersistenceService`（P6，Scribe）内部出现；其他位置禁用。
  - 工具 Tool JSON 的唯一产出方为 `IToolRegistryService`（P4）。上层仅消费，不自行拼装或向量检索。
  - 历史域（P8）仅记录“最终输出”，不落中间过程；显示名在渲染时解析，不入档。
  - Stage（P9）为“薄仲裁与路由”，不触达 LLM/Framework；所有 Act 最终总结文本统一写入 `agent:stage` 专属日志线程。

- 非流式纪律：后台/服务型路径一律非流式；流式仅用于 Debug/UI 演示（P2 明确）。

- 依赖注入：仅允许“构造函数注入”；禁止属性注入与 Service Locator。

- 键与 ID 规范：
  - `participantId`：`pawn:<loadId>` / `thing:<loadId>` / `player:<saveInstanceId>` / `persona:<name>#<rev>`；保留字 `agent:stage`。
  - `convKey = join('|', sort(participantIds))`（顺序无关、全局唯一）。
  - Persona 的 `entityId` 仅允许 `pawn:<loadId>` 与 `thing:<loadId>`。

- 配置与热重载：
  - 外部仅可见 `CoreConfigSnapshot`（不可变）；内部使用 `CoreConfig`；通过 `IConfigurationService.OnConfigurationChanged` 广播快照替换事件。

- 日志与可观测：
  - 【V5 强制】所有日志必须以 `[RimAI.Core]` 为前缀（置于最开头），推荐追加阶段标记形成层级前缀，例如：`[RimAI.Core][P1] ...`、`[RimAI.Core][P4] ...`。
  - 避免输出敏感正文；Debug 面板提供最小自检按钮与指标。

---

## 2. 目录与命名（建议）

```text
RimAI.Core/
  Source/
    Infrastructure/                 # 容器/配置/调度等横切基础设施
    Modules/                        # 功能域（LLM/Tooling/Orchestration/Persistence/Persona/History/Stage/...）
    UI/DebugPanel/Parts/            # 分阶段 Debug 面板子页签（Px_*）
RimAI.Core.Contracts/
  Config/                           # 仅暴露对外的最小快照与只读接口
```

---

## 3. Debug 面板约定

- 仅在 `UI.DebugPanelEnabled=true` 时显示；各阶段按钮/页签以 `Px_*` 命名。
- 按钮/日志前缀形如 `[RimAI.Core][P{n}]`，便于搜索与回归。

---

## 4. CI/Gate 基线（Cursor 内置工具）

- Verse/Scribe 面最小化：除 `Modules/World/**` 与 `Modules/Persistence/**` 外，检查：`\bScribe\.|using\s+Verse` → 0 次匹配。
- Framework 面最小化：除 `Modules/LLM/**` 外，检查：`using\s+RimAI\.Framework` → 0 次匹配。
- 非流式纪律：后台模块检查：`StreamResponseAsync\(` → 0 次匹配。
- 注入纪律：检查属性注入与 Service Locator 关键字（项目内约定模式）→ 0 次匹配。
- 文件 IO 集中：除 `Modules/Persistence/**` 外，检查：`using\s+System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b|\bStreamReader\b|\bStreamWriter\b` → 0 次匹配（模块不得直接文件 IO）。
- 日志前缀（建议性 Gate）：通过 Cursor 内置工具对 `Log\.(Message|Warning|Error)\(` 的调用进行抽样/审计，确保文本以 `[RimAI.Core]` 开头；必要时引入包装器统一打印。

---

## 5. 性能预算与容错（通用目标）

- 启动预热 ≤ 200ms（P1 容器）；正常帧新增 ≤ 1ms（关闭 Debug 面板）。
- 长任务/指数退避/断路器（P2）；主线程帧预算与长任务告警（P3）。
- 节点级容错与统计（P6）；索引/扫描限额与合流/冷却（P4/P9）。

---

## 6. 变更管理

- 对外快照新增字段需后向兼容，且在相应实施文档“默认配置/验收 Gate”中同步更新。
- 修改全局纪律需更新本文件并在相关 Px 文档的“通用全局纪律”段落中提及。

---

## 7. 工具索引与持久化 IO（强制约定）

- 工具索引（Tool Index）只以“设置文件（JSON）”形式持久化，不入游戏存档（Scribe）。
  - 建议路径：`Config/RimAI/Indices/tools_index_{provider}_{model}.json`。
  - 文件读写必须通过 `IPersistenceService` 提供的统一文件 IO API 完成；Tooling 模块自身不得直接文件 IO 或触达 Scribe。

- Persistence 为全局唯一持久化 IO 入口：
  - 负责 RimWorld 存档（Scribe）与设置文件（JSON）的读写与路径管理。
  - 其他模块（Tooling/Persona/History/Stage 等）如需读写文件，一律通过 `IPersistenceService`。

本文档为 V5 全局规范的唯一权威描述。提交涉及跨阶段边界的改动，请在 PR 中引用并更新本文件。


---

## 8. 新工具添加规范（Tool Authoring Conventions）

- 基本目标：保持“单一事实源、最小访问面、可索引可执行”。工具的 Tool JSON 由 `IToolRegistryService` 统一产出；执行统一经 `IToolRegistryService.ExecuteToolAsync` 入口。

- 目录与命名：
  - 工具类实现 `IRimAITool`，放置于 `Source/Modules/Tooling/**`（建议 `DemoTools/` 或按域分文件夹）。
  - 工具名 `Name` 使用 `snake_case`，描述 `Description` 使用英文短句，参数 Schema 提供 JSON（`ParametersJson`）。

- 注册与发现：
  - 工具应为可无参构造的 `internal sealed class`，实现 `IRimAITool`；由 `ToolDiscovery` 通过反射自动发现并加入注册表与索引。
  - 不允许在工具实现内部触达 Framework/Verse。若需世界数据，改为调用 P3 的 `IWorldDataService`（只读、主线程化）；若需文件 IO，改为调用 P6 的 `IPersistenceService`。

- Tool JSON 产出：
  - 通过 `IRimAITool.BuildToolJson()` 返回形如 `{ type:"function", function:{ name, description, parameters } }` 的结构化字符串。由 `ToolRegistryService` 统一汇总为 Classic/TopK 两种模式的 Tool JSON 列表。

- 执行路径：
  - 所有工具执行统一走 `IToolRegistryService.ExecuteToolAsync(toolName, args, ct)`；禁止在上层直接 new 工具实例并调用。
  - 工具执行内部可调用 P3/P6/P7/P8 等服务，但必须遵守访问边界（Verse 仅 P3/P6；Framework 仅 P2；文件 IO 仅 P6）。
  - 仅允许非流式执行；禁止在工具内发起流式调用。

- 索引与检索（P4）：
  - 索引文本来源为工具的 `Name/Description/Parameters`；由 `ToolIndexManager` 构建，持久化为设置文件（JSON），不入存档。
  - TopK 检索由 `GetNarrowTopKToolCallSchemaAsync` 提供；索引未就绪时不得自动降级，按 V5 Gate 报告错误。

- 日志与 Gate：
  - 工具实现应使用统一日志前缀 `[RimAI.Core][P4]`（或调用方所在阶段的前缀）并避免输出敏感正文。
  - Gate 要求：
    - 除 `Modules/LLM/**` 外，不得出现 `using RimAI.Framework.*`。
    - 除 `Modules/World/**`、`Modules/Persistence/**` 外，不得出现 `using Verse|\bScribe\.`。
    - 除 `Modules/Persistence/**` 外，不得直接使用 `System.IO` 文件 API。
    - 后台路径禁止 `StreamResponseAsync(`。

- 最小示例：
  - 定义：在 `Source/Modules/Tooling/DemoTools/MyTool.cs` 实现 `IRimAITool`，返回 Tool JSON。
  - 执行：在 `ToolRegistryService.ExecuteToolAsync` 的 `switch/if` 中按 `toolName` 路由，调用相应 Core 服务获取结果并返回匿名对象（纯数据）。

### 8.1 数据获取规范（Data Retrieval for Tools）

- 单一来源（Source of truth）
  - 工具需要的“世界数据”一律通过 P3 的 `IWorldDataService` 获取；严禁在工具或上层直接 `using Verse` 或触达 Scribe。
  - 存读档或设置文件的读写一律通过 P6 的 `IPersistenceService`，工具自身不得直接 `System.IO`。

- 线程与异步
  - 所有 P3 调用使用异步 API，且由 `WorldDataService` 通过 `ISchedulerService.ScheduleOnMainThreadAsync` 在主线程访问 Verse。
  - 调用方（工具执行）必须传入 `CancellationToken`；禁止 `.Wait()`/`.Result` 阻塞主线程；后台非流式。

- 快照与模型（Snapshot）
  - P3 返回不可变 POCO 快照（仅包含基础值/标志），不携带 Verse 类型或句柄；字段命名稳定、可后向兼容。
  - 规范数值：
    - 能力值等强度型字段优先归一到 0..1；若为百分比结果，则明确为 0..100。
    - 标志型字段使用 `bool`（如 `IsDead`）。

- 计算职责分离
  - P3 仅做“纯获取/适配”，不进行业务聚合或策略判断；如需平均/阈值/排序等，由工具或更上层领域逻辑完成。
  - 工具可在执行时对 P3 快照做轻量计算（如均值、阈值判断）并返回结构化结果给上层（UI/编排）。

- 可靠性与预算
  - 超时：由 `IConfigurationService` 的世界数据配置提供默认超时；P3 内部使用 `CancelAfter(...)` 统一处理。
  - 并发与节流：避免短周期高频调用；可在工具层做合流/缓存（TTL），但不得牺牲一致性或越权缓存 Verse 对象。

- 隐私与日志
  - 避免输出敏感正文；日志使用统一前缀（例如 `[RimAI.Core][P4]`/`[P3]`），记录键/哈希与指标，不落明文内容。

- Gate 对齐
  - 检查=0：除 `Modules/World/**`、`Modules/Persistence/**` 外 `using Verse|\bScribe\.`。
  - 检查=0：除 `Modules/Persistence/**` 外 `System.IO` 系列。
  - 检查=0：后台路径 `StreamResponseAsync\(`。

