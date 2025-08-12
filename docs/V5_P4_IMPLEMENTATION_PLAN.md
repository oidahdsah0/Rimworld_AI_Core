# RimAI V5 — P4 实施计划（Tool System：含内置向量索引与 Tool JSON 产出）

> 目标：交付“稳定、可扩展、可观测”的工具系统，并内置“工具向量索引（Embedding）”。Tool Service 成为 Framework Tool Calls 所需 Tool JSON 的**唯一产出方**；Classic 直接产出“全集 Tool JSON”，NarrowTopK 产出“TopK Tool JSON + 置信度分数表”。索引构建与检索完全由 Tool Service 负责，上游（编排层）仅消费该服务，不再自行拼装或做向量检索。本文档为唯一入口，无需翻阅旧文即可落地与验收。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 工具契约/注册/参数校验/执行沙箱（并发/主线程/副作用/超时/速率）
  - 工具向量索引（Embedding Index）：构建/持久化/检索/健康状态与自动重建
  - Tool JSON 产出：
    - Classic：返回可直接发送给 Framework Tool Calls 的完整 Tool JSON 列表
    - NarrowTopK：将提示转 Embedding，与工具库比对返回 TopK Tool JSON + 置信度分数表
  - 配置与事件：Embedding 供应商变更触发“立刻重建”；游戏开始自动向量化；Debug 面板索引页签

- 非目标（后续阶段）
  - 最终自然语言总结/编排策略（P5+ 与 Organizer/Prompt）
  - 任意“自动降级/自动切换”逻辑（上游显式选择模式；索引未就绪则报错，不回退）

---

## 1. 架构总览（全局视角）

- 单一事实源（SSOT）
  - Tool Service 是 Framework Tool Calls 所需 Tool JSON 的唯一产出方（Classic & NarrowTopK）
  - 向量索引完全内置在 Tool Service，负责构建/存储/检索与状态

- 依赖关系
  - 只读/主线程：依赖 `IWorldDataService`、`ISchedulerService`（P3）
  - LLM/Embedding：依赖 `ILLMService.GetEmbeddingsAsync`（P2）以调用 Framework Embedding API（Tool Service 本身不直接引用 Framework）
  - 配置：`IConfigurationService`（P1），监听 Embedding 供应商变更

- 运行原则
  - Classic 直接返回“可用工具全集”的 Tool JSON
  - NarrowTopK：以 `userInput` → Embedding → 与“工具向量库”比对 → 返回 TopK Tool JSON + 置信度分数
  - 无任何“自动切换/自动降级”；索引未就绪或无候选，返回明确错误，由上游决定后续动作

---

## 2. 接口契约（内部 API）

> Schema 类型沿用 `RimAI.Framework.Contracts.ToolFunction`（与 Framework 对齐）；接口位于 Core 内部。

```csharp
// RimAI.Core/Source/Modules/Tooling/ToolIndexModels.cs
namespace RimAI.Core.Modules.Tooling.Indexing {
  internal sealed class ToolEmbeddingRecord {
    public string Id { get; init; }                    // 记录ID（guid）
    public string ToolName { get; init; }              // 唯一真函数名（工具名）
    public string Variant { get; init; }               // name | description | parameters
    public string Text { get; init; }                  // 嵌入文本
    public float[] Vector { get; init; }               // 嵌入向量
    public string Provider { get; init; }
    public string Model { get; init; }
    public int Dimension { get; init; }
    public string Instruction { get; init; }
    public DateTime BuiltAtUtc { get; init; }
  }

  internal sealed class ToolIndexFingerprint {
    public string Provider { get; init; }
    public string Model { get; init; }
    public int Dimension { get; init; }
    public string Instruction { get; init; }
    public string Hash { get; init; }                  // Provider+Model+Dimension+Instruction 的哈希
  }

  internal sealed class ToolIndexSnapshot {
    public ToolIndexFingerprint Fingerprint { get; init; }
    public IReadOnlyList<ToolEmbeddingRecord> Records { get; init; }
    public (double Name, double Desc, double Params) Weights { get; init; }  // 评分权重
    public DateTime BuiltAtUtc { get; init; }
  }

  internal sealed class ToolScore {
    public string ToolName { get; init; }
    public double Score { get; init; }
  }
}

// RimAI.Core/Source/Modules/Tooling/IToolRegistryService.cs （扩展）
using RimAI.Framework.Contracts; // ToolFunction

namespace RimAI.Core.Modules.Tooling {
  internal sealed class ToolQueryOptions {
    public ToolOrigin Origin { get; init; } = ToolOrigin.PlayerUI;
    public IReadOnlyList<string> IncludeWhitelist { get; init; }
    public IReadOnlyList<string> ExcludeBlacklist { get; init; }
  }

  internal sealed class ToolClassicResult {
    public IReadOnlyList<ToolFunction> Tools { get; init; }             // 可直接发送到 Framework 的 Tool JSON
  }

  internal sealed class ToolNarrowTopKResult {
    public IReadOnlyList<ToolFunction> Tools { get; init; }             // TopK Tool JSON
    public IReadOnlyList<Indexing.ToolScore> Scores { get; init; }      // 置信度分数表（与 Tools 对齐或按名称）
  }

  internal interface IToolRegistryService {
    // 工具清单（Classic）
    ToolClassicResult GetClassicToolCallSchema(ToolQueryOptions options = null);

    // TopK 候选（NarrowTopK）。若索引未就绪或无候选，抛出 ToolIndexNotReadyException / 返回空集（按约定抛错更清晰）
    Task<ToolNarrowTopKResult> GetNarrowTopKToolCallSchemaAsync(
      string userInput,
      int k,
      double? minScore,
      ToolQueryOptions options = null,
      CancellationToken ct = default);

    // 工具执行（与 P4 既有定义一致）
    Task<object> ExecuteToolAsync(
      string toolName,
      Dictionary<string, object> args,
      ToolCallOptions options,
      CancellationToken ct = default);

    // 索引生命周期与状态
    bool IsIndexReady();
    Tooling.Indexing.ToolIndexFingerprint GetIndexFingerprint();
    Task EnsureIndexBuiltAsync(CancellationToken ct = default);   // 若缺失/过期则构建
    Task RebuildIndexAsync(CancellationToken ct = default);       // 强制重建
    void MarkIndexStale();                                        // 标记过期（工具变化/配置变化）
  }
}
```

> 执行契约 `IRimAITool`/`ToolSandbox` 保持原有：参数校验/主线程/速率/超时/互斥，本文不再重复。

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Tooling/
        IRimAITool.cs
        IToolRegistryService.cs
        ToolRegistryService.cs
        ToolDiscovery.cs               // 反射扫描 + DI 构造
        ToolSandbox.cs                 // 并发/主线程/互斥/速率/超时
        ToolBinding.cs                 // 参数校验
        ToolLogging.cs
        Indexing/                      // 新增：工具向量索引
          ToolIndexModels.cs          // 记录/指纹/快照/分数
          ToolIndexManager.cs         // 构建/加载/保存/检索
          ToolIndexBuilder.cs         // 调用 ILLMService.GetEmbeddingsAsync 批量向量化
          ToolIndexStorage.cs         // JSON 文件存取：tools_index_{provider}_{model}.json
        DemoTools/
          GetColonyStatusTool.cs
    UI/DebugPanel/Parts/
      P4_ToolExplorer.cs
      P4_ToolRunner.cs
      P4_ToolIndexPanel.cs            // 索引状态/重建/TopK 试算
```

---

## 4. 配置（内部 CoreConfig.Tooling 增量）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

```json
{
  "Tooling": {
    "Enabled": true,
    "DefaultTimeoutMs": 3000,
    "MaxConcurrent": 8,
    "Whitelist": [],
    "Blacklist": [],
    "DangerousToolConfirmation": false,
    "PerToolOverrides": {},
    "Embedding": {
      "Provider": "auto",
      "Model": "auto",
      "Dimension": 0,
      "Instruction": "",
      "Weights": { "Name": 0.6, "Desc": 0.4, "Params": 0.0 },
      "AutoBuildOnStart": true,
      "BlockDuringBuild": true,
      "MaxParallel": 4,
      "MaxPerMinute": 120,
      "PersistInSave": {
        "Mode": "MetaOnly",               // None | MetaOnly | FullVectors
        "MaxVectors": 2000,                 // FullVectors 时上限（避免存档过大）
        "NodeName": "RimAI_ToolingIndexV1" // Scribe 节点名（与 P6 对齐）
      }
    },
    "NarrowTopK": {
      "TopK": 5,
      "MinScoreThreshold": 0.0
    }
  }
}
```

说明：
- `Provider/Model/Dimension/Instruction` 变化会触发 `MarkIndexStale()` 并 `RebuildIndexAsync()`
- `BlockDuringBuild=true` 时，索引构建期间 NarrowTopK 相关调用被拒绝并给出明确错误
- `PersistInSave.Mode` 控制“索引入档”策略：
  - `None`：不入档（仅写外部 JSON `tools_index_{provider}_{model}.json`）。
  - `MetaOnly`（默认）：入档指纹/权重/记录数/构建时间等元信息；向量仍写入外部 JSON。
  - `FullVectors`：可选，将向量与记录一并入档（受 `MaxVectors` 限制），以实现“开档即用”但可能增大存档体积。

---

## 5. 工具向量索引（核心流程 + 与 P6 持久化整合）

### 5.1 构建（Build）

1) 收集文本：对每个已注册工具生成 2~3 条语料：`name`、`description`、（可选）`parameters` 摘要
2) 文本规范化：小写化、裁剪控制字符、合并空白、长度上限（避免过长 Schema）
3) 批量嵌入：使用 `ILLMService.GetEmbeddingsAsync`（P2）进行批量；控制并发与速率
4) 生成记录：`ToolEmbeddingRecord`（variant 指示 source）；写入 `ToolIndexSnapshot`
5) 写盘：`tools_index_{provider}_{model}.json`（含 Fingerprint/Weights/BuiltAt/Records）

与持久化（P6）协作：
- Tooling 仅写外部 JSON `tools_index_{provider}_{model}.json`；
- 是否“随档入库”由 P6 在存档阶段根据 `Embedding.PersistInSave` 配置执行（节点名同配置 `NodeName`，默认 `RimAI_ToolingIndexV1`）。

### 5.2 检索（Query）

1) 将 `userInput` 规范化后嵌入向量（同一 Provider/Model）
2) 计算与“各工具变体向量”的相似度（余弦）
3) 对同一 ToolName 汇总得分：`score = w_name*sim(name) + w_desc*sim(desc) + w_params*sim(params)`
4) 过滤与排序：按 `MinScoreThreshold` 过滤，取 TopK
5) 产出：TopK 的 `ToolFunction` 列表 + `(toolName, score)` 分数表

### 5.3 生命周期

- 自动：进入地图或第 1000 Tick 自动构建（避免加载抖动）
- 配置变化：Embedding 相关配置保存后即 `MarkIndexStale()` 并后台 `RebuildIndexAsync()`
- 工具变化：新增/删除/Schema 变更 → `MarkIndexStale()`（可选择立即重建或合并至下次自动点）
- 调用保障：当未就绪/过期且 `BlockDuringBuild=true` 时，NarrowTopK 调用直接拒绝（带原因）；Classic 不受影响

装载（由 P6 驱动注入）：
- 若 P6 注入 `ToolingIndexState`（MetaOnly/FullVectors）：
  - MetaOnly：恢复状态与指纹，随后尝试从外部 JSON 加载；缺失则后台重建。
  - FullVectors：直接恢复 Ready；若被裁剪则后台补齐/合并 JSON。
- 指纹不匹配 → 标记 Stale 并重建。

---

## 6. 实施步骤（一步到位）

> 按顺序完成 S1→S14；期间可通过 Debug 面板与日志进行自检。

### S1：契约与元数据

- 保持 `IRimAITool`/`ToolMeta`/`ToolContext` 与 P4 既有定义
- 扩展 `IToolRegistryService`：新增 Classic/TopK Tool JSON 接口与索引生命周期 API

### S2：索引模型与存储

- 新增 `ToolIndexModels.cs`/`ToolIndexStorage.cs`，实现 JSON 持久化与文件命名 `tools_index_{provider}_{model}.json`
- 指纹计算：`Provider|Model|Dimension|Instruction` SHA256 → 写入文件

（新增）快照接口：
- 在 `ToolIndexManager` 中提供：
  - `ExportSnapshot(includeVectors, maxVectors)` → `ToolingIndexState`
  - `ImportSnapshot(ToolingIndexState state)` 恢复内存结构/状态
P6 负责调用上述接口完成入档/读档；Tooling 不直接触达 `Scribe`。

### S3：索引管理器（ToolIndexManager）

- 负责装载/保存、状态（Ready/Stale/Building/Error）、并发控制与事件
- 提供 `IsReady/MarkStale/EnsureBuiltAsync/RebuildIndexAsync/GetFingerprint`

（新增）集成持久化：
- 在 `EnsureBuiltAsync` 前，若 P6 已注入快照则优先 `ImportSnapshot`；
- 在构建完成后，通过 `ExportSnapshot` 回传给 P6 以决定是否入档。

### S4：索引构建器（ToolIndexBuilder）

- 从已注册工具收集语料 → 规范化 → 通过 `ILLMService.GetEmbeddingsAsync` 批量嵌入
- 受 `MaxParallel/MaxPerMinute` 控制；失败项重试有限次

### S5：工具变化监听

- `ToolDiscovery` 完成后，对新增/删除/Schema 变化调用 `MarkIndexStale()`

### S6：配置变化监听

- 订阅 `IConfigurationService.OnConfigurationChanged`；Embedding 配置字段变化 → `MarkIndexStale()` 并 `RebuildIndexAsync()`

### S7：启动自动构建

- 进入地图或第 1000 Tick 调用 `EnsureIndexBuiltAsync()`（按配置 `AutoBuildOnStart`）

### S8：Classic Tool JSON

- 实现 `GetClassicToolCallSchema`：过滤 Origin/黑白名单/`IsAvailable` → 返回 `ToolFunction[]`

### S9：NarrowTopK Tool JSON

- 实现 `GetNarrowTopKToolCallSchemaAsync`：
  - 若 `IsIndexReady()==false`：抛 `ToolIndexNotReadyException`
  - 嵌入 `userInput` → 与索引比对 → 取 TopK/阈值过滤
  - 返回 `ToolNarrowTopKResult { Tools, Scores }`

### S10：执行沙箱与参数校验

- 延续 P4 既有实现：`ToolBinding`（JSON Schema 校验）、`ToolSandbox`（主线程/速率/超时/互斥）

### S11：日志与事件

- 统一前缀 `[RimAI.P4.Tool]`；新增索引事件 `ToolIndexRebuilding/Ready/Error`
- TopK 调用日志包含 provider/model/TopK/阈值/命中分数摘要

### S12：Debug 面板增强

- 新增 `P4_ToolIndexPanel`：
  - 状态卡：Ready/Stale/Building/Error、Fingerprint、记录数、维度、构建耗时
  - 动作：重建索引/标记过期/打开目录
  - TopK 试算：输入文本 → 展示 TopK 工具 + 分数表 + Tool JSON 预览
  - 存档策略：显示 `PersistInSave.Mode/MaxVectors` 与“来自 P6 的快照状态”（是否注入、记录数、指纹是否匹配）；Scribe 写入操作统一在 P6 面板执行。

### S13：DI 注册

- `ServiceContainer.Init()` 注册索引组件与事件订阅；启动打印索引状态摘要

### S14：边界与异常

- `BlockDuringBuild=true` 时，NarrowTopK 请求在构建期间一律拒绝（错误码 `index_building`）
- 嵌入失败重试，超过阈值后记录并跳过该记录；整体构建继续

---

## 7. 验收 Gate（必须全绿）

- Classic
  - 能返回“可用工具全集”的 `ToolFunction[]`，Framework 可直接消费
- 索引构建
  - 首次进入地图后自动构建完成；Debug 页签显示 Fingerprint/记录数/耗时
  - 修改 Embedding 配置后自动重建；日志与面板状态可观测
- NarrowTopK
  - TopK 试算：输入文本可得到 TopK 工具与分数表；阈值生效；当未就绪/构建中/无候选返回明确错误
  - 返回 Tool JSON 可直接用于上游编排的 Tools 列表
- 执行与沙箱
  - 仍可执行演示工具（只读）并通过参数校验与主线程策略

---

## 8. 回归脚本（人工/录屏）

1) 启动 → 观察索引自动构建日志与 Debug 状态
2) Tool Explorer 查看全集 Tool JSON（Classic）
3) TopK 试算：输入“殖民地概况”，展示 TopK + 分数，预览 Tool JSON
4) 修改 Embedding Provider/Model → 保存 → 观察“标记过期→重建→就绪”全流程
5) 开启 BlockDuringBuild=true → 重建过程中触发 TopK → 显示 `index_building`

---

## 9. CI/Grep Gate（必须通过）

- Verse 访问最小面：`Modules/Tooling/**` 与 `DemoTools/**` 不得 `using Verse`
- Framework 访问最小面：Tooling 中不得 `using RimAI.Framework.*`；统一经 `ILLMService` 获取 Embeddings
- 自动降级禁用：全仓 grep 确认 `\bAuto\b|degrad|fallback` 不出现在 Tooling 索引/TopK 路径
- 索引文件命名：构建后应存在 `tools_index_{provider}_{model}.json`
 - 持久化纪律：Tooling 不得直接触达 `Scribe`；入档/读档由 P6 统一完成。
---

## 10. 风险与缓解

- 构建风暴/阻断体验：默认在进入地图/第1000 Tick 构建；可关闭阻断或降低并发
- 分数不稳定：采用权重与文本规范化；必要时加入分数平滑或最小阈值
- 工具频繁变化：标记过期但可延迟到下次稳定窗口构建；提供手动重建
- Embedding 费用：依赖 P2 的缓存与速率限制；对文本做去重与摘要裁剪
- 存档膨胀：默认 `MetaOnly`，仅在明确需要“开档即用”时启用 `FullVectors`，并受 `MaxVectors` 与向量压缩策略控制。

---

## 11. FAQ（常见问题）

- Q：为什么索引构建在 Tool Service 而非编排层？
  - A：单一事实源，减少耦合；编排只消费 Tool JSON/TopK 结果，职责清晰。
- Q：NarrowTopK 未就绪怎么办？
  - A：返回明确错误（如 `index_building`/`index_not_ready`/`no_candidates`），由上游决定 UI/策略，不做回退。
- Q：权重如何设置？
  - A：默认 Name:0.6, Desc:0.4, Params:0.0；可在配置中调整并热生效（重建后生效）。

- Q：为什么提供“索引入档”能力？
  - A：在部分环境中希望“开档即用”（例如离线/慢盘/受限环境），允许将元信息或向量裁剪后随档保存，减少每次读档的冷启动时间。

- Q：入档与外部 JSON 如何协同？
  - A：优先使用入档内容恢复状态；若为 `MetaOnly` 或 `FullVectors` 被裁剪的情况，再尝试加载外部 JSON；两者指纹不一致则标记 Stale 并重建。

---

## 12. 变更记录

- v5-P4（重制）：将“工具向量索引 + Tool JSON 产出”完全下沉到 Tool Service；上游仅消费 Classic/NarrowTopK 所需产出物；移除任何“自动降级/切换”表述。
- v5-P4（增量）：新增“索引入档”能力（`Embedding.PersistInSave`），配合 P6 的 Scribe 节点 `RimAI_ToolingIndexV1`，支持 MetaOnly/FullVectors 两种策略。

---

本文件为 V5 P4 唯一权威实施说明。实现与验收以本文为准。
