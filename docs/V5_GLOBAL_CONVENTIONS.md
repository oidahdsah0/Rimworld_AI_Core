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


