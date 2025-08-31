# RimAI V5 — P2 实施计划（LLM Gateway）

> 目标：一次性交付“稳定、可观测”的 LLM 网关层（ILLMService），统一承载 Core 内部所有 Chat/Embedding 调用，全面封装 `RimAI.Framework` API，处理流式/非流式、取消/超时、错误映射与重试/断路。本文档为唯一入口文档，无需查阅旧文即可落地 P2 与完成验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 新增内部接口与实现：`ILLMService` / `LLMService`（唯一允许 `using RimAI.Framework.*` 的位置）
  - Chat 能力：非流式、流式、批量
  - Embedding 能力：非流式
  - Cache 失效：按会话 ID 精准失效（透传 Framework）
  - 可靠性：重试（指数退避）、断路器（provider+model 维度）、统一错误映射
  - 可观测：统一日志（开始/首包/每 N 分片/结束/失败）、可选内部事件、Debug 面板 P2 专用按钮
  - 配置：内部 `CoreConfig.LLM` 节点与默认值（对外 Snapshot 不新增字段）

- 非目标（后续阶段处理）
  - 工具仅编排/策略/RAG（P9/P12 等）
  - 提示词组织与模板（Prompt/Organizer）
  - 历史写入/舞台/事件聚合（P3+）

---

## 1. 架构总览（全局视角）

- 稳定边界
  - 对外（第三方 Mod）：不暴露 LLM 网关接口
  - 对内（Core）：UI/Stage/Organizer/Orchestration 仅通过 `ILLMService` 调用 LLM/Embedding

- 设计原则
  - 透传 `RimAI.Framework.Contracts` DTO（不重复定义 Chat/Embedding DTO）
  - 流式仅用于 UI/Debug 展示；后台/服务型调用固定非流式
  - 缓存/合流由 Framework 负责；Core 不做缓存
  - 取消/超时必须自上而下透传；网关提供默认超时

---

## 2. 接口契约（内部 API）

> 下述接口位于 Core 内部，不进入 Contracts 稳定层。

```csharp
// RimAI.Core/Source/Modules/LLM/ILLMService.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Framework.Contracts; // UnifiedChatRequest/Response/Chunk/EmbeddingRequest/Response

namespace RimAI.Core.Modules.LLM {
  internal interface ILLMService {
    // Chat — 非流式
    Task<Result<UnifiedChatResponse>> GetResponseAsync(
      UnifiedChatRequest request,
      CancellationToken cancellationToken = default);

    // Chat — 流式
    IAsyncEnumerable<Result<UnifiedChatChunk>> StreamResponseAsync(
      UnifiedChatRequest request,
      CancellationToken cancellationToken = default);

    // Chat — 批量
    Task<List<Result<UnifiedChatResponse>>> GetResponsesAsync(
      List<UnifiedChatRequest> requests,
      CancellationToken cancellationToken = default);

    // Embedding — 非流式
    Task<Result<UnifiedEmbeddingResponse>> GetEmbeddingsAsync(
      UnifiedEmbeddingRequest request,
      CancellationToken cancellationToken = default);

    // 会话缓存失效（透传）
    Task<Result<bool>> InvalidateConversationCacheAsync(
      string conversationId,
      CancellationToken cancellationToken = default);
  }
}
```

接口约束：
- `request.ConversationId` 必填（调用方保证）；Debug 面板可注入临时 GUID 用于演示
- 流式调用仅供 UI/Debug；后台统一使用非流式接口

---

## 3. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      LLM/
        ILLMService.cs
        LLMService.cs              // 唯一允许引用 RimAI.Framework.* 的实现
        LlmPolicies.cs             // Retry/Breaker/Timeout 策略封装
        LlmLogging.cs              // 日志/事件帮助器
    UI/DebugPanel/Parts/
      LLM_PingButton.cs
      LLM_StreamDemoButton.cs
      LLM_JsonModeDemoButton.cs
      LLM_EmbeddingTestButton.cs
      LLM_InvalidateCacheButton.cs
```

---

## 4. 配置（内部 CoreConfig.LLM）

> 配置节点存在于内部 `CoreConfig`，通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段。

建议默认值：

```json
{
  "LLM": {
    "Locale": "zh-Hans",
    "DefaultTimeoutMs": 15000,
    "Stream": { "HeartbeatTimeoutMs": 15000, "LogEveryNChunks": 20 },
    "Retry": { "MaxAttempts": 3, "BaseDelayMs": 400 },
    "CircuitBreaker": { "ErrorThreshold": 0.5, "WindowMs": 60000, "CooldownMs": 60000 },
    "Batch": { "MaxConcurrent": 4 }
  }
}
```

说明：
- Provider/Model/API Key 仍由 `RimAI.Framework` 统一管理；Core 不重复存储

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S10，期间可通过 Debug 面板与日志进行自检。无需翻阅其他文档。

### S1：新建接口与实现骨架

1) 创建 `ILLMService.cs` 与空实现 `LLMService.cs`
2) 在实现中引入：`using RimAI.Framework.API; using RimAI.Framework.Contracts;`
3) 构造函数注入：`IConfigurationService`（读取内部 LLM 配置）、日志器（可选）

### S2：请求适配与参数校验

1) 透传 `UnifiedChatRequest/UnifiedEmbeddingRequest`，不自定义 DTO
2) 入口校验：`ConversationId` 非空（Chat）、`Messages` 非空（Chat）、`Input` 非空（Embedding）
3) 自动补全：缺失 `Locale` → 使用 `CoreConfig.LLM.Locale`

### S3：非流式 Chat 实现

1) 包装调用：`RimAIApi.GetCompletionAsync(request, ct)`
2) 失败时将 `Result.Error` 映射为 Core 异常（`LLMException/ConnectionException/TimeoutException`）并返回失败 `Result`
3) 应用默认超时：若 `ct` 未设超时，创建 `CancellationTokenSource` + `CancelAfter(DefaultTimeoutMs)`
4) 重试策略：对连接类/5xx 错误按 `MaxAttempts` + `BaseDelayMs` 指数退避；4xx 不重试

### S4：流式 Chat 实现

1) 包装调用：`RimAIApi.StreamCompletionAsync(request, ct)`
2) 迭代消费：产出 `IAsyncEnumerable<Result<UnifiedChatChunk>>`
3) 首包失败重试：同 S3，首块之前允许重试；进入流后发生错误直接失败退出
4) 心跳超时：若超过 `HeartbeatTimeoutMs` 未收到分片，取消请求并返回失败
5) 日志节流：每 `LogEveryNChunks` 记录一次进度，末块记录 `FinishReason`

### S5：批量 Chat 实现

1) 包装调用：`RimAIApi.GetCompletionsAsync(requests, ct)`
2) 并行节流：按 `Batch.MaxConcurrent` 控制并发，超过进入队列
3) 汇总返回 `List<Result<UnifiedChatResponse>>`

### S6：Embedding 实现

1) 包装调用：`RimAIApi.GetEmbeddingsAsync(request, ct)`
2) 与 S3 同步的超时/重试/错误映射策略

### S7：缓存失效透传

1) `InvalidateConversationCacheAsync(conversationId, ct)` → `RimAIApi.InvalidateConversationCacheAsync`
2) 记录操作日志：会话散列（避免日志泄密）+ 成功/失败

### S8：断路器实现（provider+model 维度）

1) 维护状态：窗口内失败/总数与打开/半开/关闭状态
2) 开启条件：窗口 60s 错误率 > 50% → 打开；冷却 60s 后半开探测一次
3) 命中断路：直接返回失败 `Result`（`LLMException`，Reason=CircuitOpen）

### S9：日志与事件

1) 日志统一前缀：`[RimAI.Core][P2.LLM]`
2) 记录字段：provider/model（由 Framework 提供或“current”）、convId 哈希、请求/首包/结束耗时、分片数、错误码
3) 可选事件：`LlmRequestStarted/ChunkReceived/LlmRequestFinished/LlmRequestFailed`（供 Debug 订阅）

### S10：DI 注册与启动检查

1) 在 `ServiceContainer.Init()` 注册：`ILLMService -> LLMService`
2) 启动时 `Resolve<ILLMService>()` 自检，记录“LLM 网关已就绪”

---

## 6. Debug 面板（P2 专用按钮）

- `LLM_PingButton`：构造最小 `UnifiedChatRequest`（system+user），调用 `GetResponseAsync`，在 UI 打印完成耗时与 `FinishReason`
- `LLM_StreamDemoButton`：相同请求走 `StreamResponseAsync`，逐块渲染，末块显示原因与分片统计
- `LLM_JsonModeDemoButton`：构造 JSON 模式请求（`ResponseFormat="json_object"` 或等价字段），验证非流式 JSON 返回
- `LLM_EmbeddingTestButton`：对一段固定文本执行 `GetEmbeddingsAsync`，打印维度/向量数/Top1 范数
- `LLM_InvalidateCacheButton`：输入框填写 `ConversationId`，调用 `InvalidateConversationCacheAsync`

> 注意：上述按钮严禁写历史；仅用于演示与诊断。

---

## 7. 验收 Gate（必须全绿）

- 基础能力
  - 非流式 Echo：≤ 1.5s 首包；完整返回 `FinishReason`；错误路径清晰
  - 流式 Demo：分片持续推进；心跳超时可触发取消；末块含 `FinishReason`
  - Embedding：返回维度与向量数量正确；日志打印 Top1 向量范数
  - Cache 失效：相同 `ConversationId` 命中后可被精准失效（观察“伪流式”立即直出）

- 可靠性
  - 断网/超时/5xx：重试与断路器行为符合策略；日志可见状态迁移（Closed→Open→HalfOpen→Closed）
  - 4xx/校验错误：不重试、即时失败并映射正确异常

- 可观测
  - 日志包含 provider/model/convId 哈希/latency/chunks 与错误摘要
  - Debug 面板按钮运行正常且可录屏复现

- 性能
  - 并发 4 时稳定无 UI 卡顿；每帧新增 ≤ 1ms（仅日志与事件）

---

## 8. 回归脚本（人工/录屏）

1) 打开 Debug 面板 → 运行 `LLM_PingButton`，期望 1–1.5s 内返回
2) 运行 `LLM_StreamDemoButton`：观察分片推进与末块原因
3) 运行 `LLM_JsonModeDemoButton`：验证 JSON 返回格式正确
4) 运行 `LLM_EmbeddingTestButton`：记录维度/Top1 范数
5) 重复 1) 两次 → 运行 `LLM_InvalidateCacheButton` → 重复 1) 验证命中失效
6) 断网重试与断路器：断开网络/模拟 5xx，观察重试与断路器日志

---

## 9. CI/Gate（使用 Cursor 内置工具，必须通过）

- 唯一 Framework 入口：`RimAI.Core/Source/Modules/LLM/LLMService.cs`
  - 全仓检查：`using\s+RimAI\.Framework(?!.*Modules/LLM/LLMService\.cs)` → 0
- 禁止业务层直触流式/非流式：业务层仅引用 `ILLMService`（可抽样静态分析）
- 构造函数注入：检查属性注入/ServiceLocator 滥用 → 0

---

## 10. 风险与缓解

- Provider/Model 变更导致行为波动 → 透传 Framework 当前激活配置；日志打印实际使用的 provider/model
- 断路器误触发 → 调整 `ErrorThreshold/WindowMs/CooldownMs`，或对特定错误类型白名单
- 流式心跳误判 → 适当增大 `HeartbeatTimeoutMs` 并在 Debug 记录“最后收到分片的时间戳”
- 费用与速率限制 → 依赖 Framework 的缓存/合流；上层增加限流与录屏复现脚本

---

## 11. FAQ（常见问题）

- Q：`ConversationId` 为什么必填？
  - A：Framework 的会话级缓存与伪流式依赖此键；缺失会导致缓存/合流效果丢失且 Debug 难以复现。
- Q：后台是否允许流式？
  - A：不允许。后台/服务型路径固定非流式，避免资源占用与 UI 干扰。
- Q：断路器与重试的关系？
  - A：先按重试策略执行；失败率在窗口内超过阈值时进入断路，冷却期内直接失败；半开探测成功才闭合。
- Q：是否支持工具/JSON/TTS 等扩展？
  - A：支持通过请求透传，但策略/模板/编排不在 P2 实施范围；后续阶段接入。

---

## 12. 变更记录（提交要求）

- 初版（v5-P2）：交付 LLM 网关/可靠性/Debug 按钮/CI Gate；不改对外 Contracts
- 后续修改：若新增内部配置字段，需向后兼容并在本文“配置”与 Gate 中同步更新

---

本文件为 V5 P2 唯一权威实施说明。实现与验收以本文为准。
