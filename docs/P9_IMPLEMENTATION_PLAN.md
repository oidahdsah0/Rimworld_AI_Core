# P9 施工方案（方案B）— 编排策略层并行：Classic 与 EmbeddingFirst

文档状态：施工计划（实施前置）

## 0. 背景与目标
- 背景：在 V4 P8 完成基础上，引入“Embedding 为强制能力”的检索增强（RAG）流程；同时保留现有 Tool System + Orchestration 的工作流。
- 目标：以“策略层并行”的方式实现 Embedding 与经典编排的共存与可切换，保持对外接口稳定（不破坏 `IOrchestrationService`）。

不做什么（非目标）：
- 不更改对外 Contract 接口签名（`IOrchestrationService` 等）。
- 不强制修改 Persona 模型结构（授权工具列表后续版本推进）。

## 1. 架构概览
新增“编排策略层”，将具体编排流程抽象为策略。

- `IOrchestrationStrategy`（内部接口）：定义统一的执行入口。
- `ClassicStrategy`：搬迁现有五步工作流的实现（Tools 决策 → 执行 → 复述总结 → 流式返回）。
- `EmbeddingFirstStrategy`：在 Step 0 前置 RAG 流程（Embedding + 相似度检索 + 上下文注入），之后与 Classic 同步走工具与总结环节。
- `OrchestrationService`：保留为唯一对外入口，根据配置/Persona 选择策略并委派执行。

依赖关系：
- `OrchestrationService` → `IOrchestrationStrategy`（按策略名解析）
- `EmbeddingFirstStrategy` → `IEmbeddingService`、`IRagIndexService`、`ILLMService`、`IToolRegistryService`、`ICacheService`、`IConfigurationService`
- `ClassicStrategy` → `ILLMService`、`IToolRegistryService`、`ICacheService`

## 2. 新增/调整的组件与接口（草案）
以下接口与类型均置于 Core 内部命名空间（不进入 Contracts 稳定层），保持对外 API 稳定。

### 2.1 Orchestration 策略接口
```csharp
internal interface IOrchestrationStrategy
{
    string Name { get; } // "Classic" | "EmbeddingFirst"
    IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteAsync(OrchestrationContext context);
}

internal sealed class OrchestrationContext
{
    public string Query { get; init; }
    public string PersonaSystemPrompt { get; init; }
    public CancellationToken Cancellation { get; init; } // 可选

    // 预留：按需传入策略所需的额外元数据
    public IReadOnlyDictionary<string, object> Metadata { get; init; }
}
```

### 2.2 Embedding 与 RAG
```csharp
internal interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text);
}

internal interface IRagIndexService
{
    // 写入/更新文档（若 embedding 未提供则内部计算）
    Task UpsertAsync(string docId, string content, float[] embedding = null);
    // 基于查询向量检索最相似的 topK 文档
    Task<IReadOnlyList<RagHit>> QueryAsync(float[] queryEmbedding, int topK);
}

internal sealed class RagHit
{
    public string DocId { get; init; }
    public string Content { get; init; }
    public float Score { get; init; }
}
```

实现建议：
- `EmbeddingService` 直接调用 `RimAI.Framework` 的 Embedding API；对输入做去重与缓存（`ICacheService`，key=`embed:sha256(text)`，TTL 默认 60 分钟）。
- `RagIndexService` 先实现为内存向量索引（List + 余弦相似度），并在首期不做持久化（加载后可按需暖身/延迟构建）。

### 2.3 OrchestrationService 委派
```csharp
internal sealed class OrchestrationService : IOrchestrationService
{
    // 通过配置/Persona 决定策略，再委派执行
    public IAsyncEnumerable<Result<UnifiedChatChunk>> ExecuteToolAssistedQueryAsync(string query, string personaSystemPrompt = "")
    {
        var strategyName = _config.Current.Orchestration.Strategy; // Classic | EmbeddingFirst
        var strategy = _strategies[strategyName] ?? _strategies["Classic"]; // 安全回退
        var ctx = new OrchestrationContext { Query = query ?? string.Empty, PersonaSystemPrompt = personaSystemPrompt ?? string.Empty };
        return strategy.ExecuteAsync(ctx);
    }
}
```

## 3. EmbeddingFirstStrategy 流程细节
Step 0: RAG 预处理（新增）
1) 对 `context.Query` 计算 embedding → 查询 `IRagIndexService.QueryAsync(topK)`
2) 生成“检索上下文块”，注入到消息序列前（作为 system 或 assistant 提示，形式示例：
   - System: "以下为与当前请求高度相关的内部上下文（可能包含历史摘要/知识片段/任务状态），回答时尽量引用要点：\n- [DocA] ...\n- [DocB] ..."
3) 合并上下文后，继续原有工具流程：
   - Step 1: 暴露工具 schema → LLM 决策 tool_calls
   - Step 2: 本地执行工具（异常时构造错误提示并直接流式复述）
   - Step 3: 携带工具结果跟进请求（流式）
   - Step 4: 结果缓存（key 包含检索上下文哈希/文档ID集合）

缓存键建议：
```
summ:sha256(query + toolResultJson + join(sorted(docIds)))
```

异常与回退：
- Embedding/RAG 任一步失败 → 记录日志 → 降级为无上下文（仅 PersonaPrompt），随后继续工具流程；如仍失败则返回错误 Result。

### 3.5 工具检索（LightningFast / FastTop1 / NarrowTopK）并行设计
三种工具匹配方案与经典方案并存，并由用户在 UI/配置中切换：

- LightningFast（闪电匹配）：
  1) 使用工具向量库对“可用工具”做 Top1 检索，需满足更高置信阈值（如 LightningTop1Threshold ≥ Top1Threshold）。
  2) 仅暴露该工具的 function schema，请 LLM 生成参数 JSON；校验失败有限重试（如 2 次），仍失败降级至 FastTop1 或 NarrowTopK。
  3) 在调用本地工具时，自动追加保留参数 `__fastResponse=true`（或 `_fast=true`）以通知工具走“快速文本响应”路径。
  4) 工具返回“已装填好的字符串”（面向玩家的最终文本）。编排层直接将该字符串切片为 `UnifiedChatChunk` 增量，模拟流式输出到 UI（无需进行第二次 LLM 总结）。
  5) 回退策略：若工具未返回字符串（返回对象等），则将结果序列化为紧凑 JSON 或简短模板文本；必要时可退回“跟进请求由 LLM 总结”的路径。

- FastTop1（快匹配）：
  1) 将每个工具的 Name + Description + Parameters（展开 schema 的参数名/描述）拼接为统一语料，预计算向量并保存为小型向量库（本地文件）。
  2) 用户问题 → 计算查询向量 → 命中 Top1 工具（先经过“可用性过滤”：人格授权 + 硬件解锁/通电 + 世界状态可用性）。
  3) 仅暴露该工具的 function schema，请 LLM 生成参数 JSON；参数校验失败则有限重试（如 2 次），仍失败自动降级到 NarrowTopK 或 Classic。
  4) 执行工具 → 拼接下一阶段提示词（包含工具结果）→ 发给 LLM → 流式返回最终结果。
  5) 置信阈值不足（如 cosine < Top1Threshold）自动降级到 NarrowTopK。

- NarrowTopK（正常匹配）：
  1) 同样基于工具向量库，对“可用工具”检索出 TopK 候选。
  2) 将这份“缩小后的工具列表”转为标准 Tools 表（function definitions），进入经典工具匹配方案，由模型自行选择调用与多轮跟进。

- Classic：
  - 保持现状：直接暴露“全部可用工具”进入经典匹配；作为降级路径与保底模式。

- Auto（可选）：
  - 按“FastTop1 → NarrowTopK → Classic”的顺序自动决策与降级。

实现补充：
- 工具向量库以“provider+model”维度独立存储，避免跨模型混用；文件名示例：`tools_index_{provider}_{model}.json`。
- 向量库重建触发条件：
  1) 工具清单变更（新增/删除/Schema 变化）
  2) Embedding 配置变更（详见“6.5 索引与重建规则”）
  3) 索引文件缺失或指纹不匹配
 - 工具快速响应标志：不修改 `IRimAITool` 签名，通过参数字典传入保留键 `__fastResponse=true`；工具可选择支持该模式并返回面向玩家的最终字符串。

## 4. 数据来源与索引灌入
最小可行策略（P9）：
- 索引来源：历史对话摘要（`IHistoryService` 的 ConversationEntry）、Persona 说明扩展、必要的系统知识片段。
- 灌入方式：
  - 首期采用“懒构建”：第一次查询时读取最近 N 条历史对话，批量生成 embedding 并 Upsert。
  - 或在 `HistoryService.RecordEntryAsync` 成功后异步调用 `IRagIndexService.UpsertAsync`（注意非阻塞，不在主线程）。

持久化（P9 暂不落盘）：
- 不新增存档结构；向量可在每次读档后按需重建（可配置是否自动重建，避免加载抖动）。

## 5. 配置项（CoreConfig 新增）
```json
Orchestration: {
  Strategy: "EmbeddingFirst" // 或 "Classic"
  ,
  Clarification: {
    Enabled: true,            // 低置信或歧义时，先提1轮澄清问题
    MinConfidence: 0.75,      // 低于该值触发澄清
    MaxTurns: 1               // 最多澄清1轮（避免对话拖长）
  },
  Planning: {
    EnableLightChaining: true, // 轻量多工具串联（最多2-3步）
    MaxSteps: 3
  },
  Safety: {
    MaxTokensPerQuery: 2048,   // 每次请求Token上限
    MaxLatencyMs: 30000,       // 单次请求上限时长
    MonthlyBudgetUSD: 5.0,     // 费用护栏（可选）
    CircuitBreaker: {          // 断路器
      ErrorThreshold: 5,
      CooldownMs: 60000
    }
  }
},
Embedding: {
  Enabled: true,
  Model: "auto",
  TopK: 5,
  MaxContextChars: 2000,
  CacheMinutes: 60,
  Tools: {
    Mode: "Auto",            // 可选: Classic | LightningFast | FastTop1 | NarrowTopK | Auto
    Top1Threshold: 0.82,      // FastTop1 的相似度阈值（余弦）
    LightningTop1Threshold: 0.86, // 闪电匹配 Top1 的更高置信阈值
    IndexPath: "auto",        // 工具向量索引的存储路径（auto=默认配置目录）
    DynamicThresholds: {
      Enabled: true,          // 动态阈值，根据历史命中质量微调
      Smoothing: 0.2,         // 平滑系数（0~1）
      MinTop1: 0.78,
      MaxTop1: 0.90
    }
  }
}
```
实现：
- `ConfigurationService` 增加默认值与热重载；`OrchestrationService` 在每次调用时读取当前快照。
 - 人格主 Tab 或设置界面提供模式切换（Classic/FastTop1/NarrowTopK/Auto）与“重建索引”按钮。
 - UI 同步显示并可切换 LightningFast；若当前置信度不足或工具不支持快速响应，将自动降级并在日志提示。
 - UI 增加人工干预：
   - 工具强制选择（覆盖自动匹配）/ 工具黑白名单（会话或全局）；
   - Panic Switch 一键关闭 LightningFast/Embedding；
   - 成本/时延小部件（显示本轮Token/费用/耗时）。

## 6. DI 注册与文件结构
- 目录建议：
  - `Source/Modules/Orchestration/Strategies/IOrchestrationStrategy.cs`
  - `Source/Modules/Orchestration/Strategies/ClassicStrategy.cs`
  - `Source/Modules/Orchestration/Strategies/EmbeddingFirstStrategy.cs`
  - `Source/Modules/Embedding/IEmbeddingService.cs` + `EmbeddingService.cs`
  - `Source/Modules/Embedding/IRagIndexService.cs` + `RagIndexService.cs`
  - `Source/Modules/Embedding/IToolVectorIndexService.cs` + `ToolVectorIndexService.cs`
- DI：在 `ServiceContainer.Init()` 中注册：
  - `IEmbeddingService -> EmbeddingService`
  - `IRagIndexService -> RagIndexService`
  - `IToolVectorIndexService -> ToolVectorIndexService`
  - `IOrchestrationStrategy(Classic)`、`IOrchestrationStrategy(EmbeddingFirst)`：可采用命名字典或注册工厂；简单实现可在 `OrchestrationService` 构造中注入 `IEnumerable<IOrchestrationStrategy>` 并转为字典。

### 6.5 索引与重建规则（重点）
工具向量库（Name+Description+Parameters 统一语料的 embedding 小型库）的重建判定：

- 触发时机：
  1) 工具集变化：`ToolRegistryService` 在扫描到工具新增/删除/Schema 变化时标记“索引已过期”。
  2) 用户“更换 Embedding 配置并点击保存”后（以保存动作为准）：
     - 一旦保存，视为 Embedding 配置已生效，必须对“所有工具”重新计算并写入向量（全量 Re-Embedding）。
  3) 运行时检测索引文件缺失或与当前 Embedding 指纹不一致。

- 指纹（Fingerprint）：
  - 由 `{provider, model, dimension, pooling, instruction}` 等关键字段计算哈希；存入索引文件旁的元数据（或同一 JSON 内）。
  - 加载时若指纹不匹配，强制重建。

- 存储与命名：
  - 以 `provider+model` 维度命名文件：`tools_index_{provider}_{model}.json`；
  - 内容包含：工具ID、统一语料、embedding 向量（可选量化）、Schema 摘要、指纹、最后更新时间。

- 可见性：
  - UI 提供“重建索引”按钮与进度提示；日志打印“开始重建/完成/错误信息”。

## 7. UI 与可观测性（最小）
- 人格主 Tab：显示当前全局策略（Classic/EmbeddingFirst），提供只读状态与“重建索引”按钮（可选）。
- 日志：在关键步骤打印 Info（命中 topK 文档 ID、缓存命中、降级路径）。

## 8. 验收标准（Gate）
1) 配置切换策略无须重启游戏（热重载生效）。
2) EmbeddingFirst：在无工具参与的问题上，相较 Classic 出现更高的上下文相关性（人工可验证）。
3) 发生 Embedding/RAG 故障时，自动回退 Classic 路径并给出日志提示。
4) 性能：首次查询可接受冷启动（≤ 1.5s），同场景二次查询 ≤ 400ms（含缓存命中）。
5) 当用户更换 Embedding 配置并保存后，立即触发“全量 Tool Re-Embedding”，并在日志中清晰记录（包含旧/新指纹、耗时、重建工具数）。
6) LightningFast：Top1 置信度满足阈值，工具返回字符串并以流式方式呈现，无需第二次 LLM 跟进；当参数生成失败/不满足阈值/工具不支持时自动降级（FastTop1→NarrowTopK→Classic）。
7) 低置信澄清：当置信度低或参数不确定性高时，先进行≤1轮澄清问答；用户回答后再继续匹配流程。
8) 多工具串联：在 NarrowTopK 不确定或任务涉及步骤分解时，支持≤3步轻量串联（例如：侦测→汇总→建议），并产生连贯的最终输出。
9) 执行校验：工具返回结果后执行最小一致性/范围/单位校验；失败时回退到 LLM 总结或切换 Classic；日志包含校验失败原因。
10) 动态阈值：阈值根据近期命中质量自动微调（启用时），并遵守上下界；异常时自动回退默认值。
11) 人为干预：UI 可强制选择工具或禁用指定工具；设置在会话内即时生效。
12) 安全护栏：遵守 Token、时延、费用上限；断路器触发后进入冷却并在 UI/日志提示。

## 9. 任务拆解与里程碑
- M1：接口与骨架
  - 新增 `IOrchestrationStrategy` 与 `OrchestrationContext`
  - 拆分 `ClassicStrategy`，验证编译与现有回归（无行为变化）
- M2：Embedding 集成
  - `IEmbeddingService`、`IRagIndexService`、`EmbeddingFirstStrategy`
  - 注入 RAG Step 0、构建上下文注入、缓存键扩展
- M2.5：工具向量库与模式切换
  - `IToolVectorIndexService` 与持久化文件
  - LightningFast / FastTop1 / NarrowTopK 流程接入、阈值与降级
  - 变更保存触发“全量 Tool Re-Embedding”
- M3：数据灌入与可观测
  - 历史对话懒索引/异步索引；主 Tab 显示状态与“重建索引”（可选）
- M4：打磨与回归
  - 性能优化与降级路径打通；对比 Classic/EmbeddingFirst 效果录像

## 10. 回滚方案
- 配置 `Orchestration.Strategy = "Classic"` 即刻回退。
- 关闭 `Embedding.Enabled` 禁用所有 Embedding 调用（策略自动视为 Classic 流程）。

## 11. 风险与缓解
- 成本与性能：Embedding 费用与延迟上升 → 结果缓存 + 文本去重 + 异步批处理。
- 兼容性：保持 Contracts 不变；策略为内部实现细节。
- 可维护性：策略隔离，便于 A/B 与未来策略（如 Event-Driven）。

## 12. 预估工期（理想人日）
- M1：0.5d
- M2：1.5d
- M3：1.0d
- M4：0.5d

> 备注：本计划严格遵循 V4 的“渐进式交付 + 界面可验证”原则，每个里程碑都应附带短录像与最小演示脚本。

## 13. 长尾场景与护栏（补充设计）

- 多工具串联（轻量规划）：
  - 触发条件：NarrowTopK 不确定（分数接近/跨度小）、或用户意图明显包含多动作；
  - 策略：最多 2-3 步（如：检索→执行→总结），每步都可缓存与中断；
  - 输出：保证最终话术连贯，并明确列出步骤与结果来源（可选）。

- 低置信澄清：
  - 当 Top1/参数置信低于阈值时，先以简短问题澄清；
  - 限制 1 轮，避免对话过长；支持用户跳过澄清并继续。

- 执行校验：
  - 结果字段必备、取值范围/单位检查、空结果/异常路径处理；
  - 失败回退：转 LLM 总结或 Classic，保障玩家体验不中断。

- 动态阈值与降级：
  - 根据近期命中质量指标（成功率、用户纠正率）微调阈值，平滑更新；
  - 全链路降级顺序：LightningFast → FastTop1 → NarrowTopK → Classic；
  - 超时/高错误率触发断路器，进入冷却后再恢复。

- 人为干预与名单：
  - UI 支持强制选工具/禁用工具（会话级/全局）；
  - 黑白名单与 Persona 授权结合，优先过滤不可用工具。

- 可观测性与安全：
  - 记录命中分数、阈值、降级原因、澄清触发、串联步数、耗时与费用；
  - 提供 Panic Switch 和上限护栏；所有 Embedding/LLM 调用在后台线程执行，主线程仅泵 UI。


