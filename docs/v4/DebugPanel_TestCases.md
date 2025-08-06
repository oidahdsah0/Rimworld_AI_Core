# RimAI Debug 面板调用示例

> 文档版本：**P3**（对应 `MainTabWindow_RimAIDebug.cs` 当前实现）  
> 最近更新：2025-08-06
>
> 该文件详细列出了调试面板中 **8** 个按钮的完整调用链、请求结构与期望输出。
> 目的：
> 1. 为新开发者提供 **Core ↔ Framework ↔ LLM** 三层交互的最小可复现示例；  
> 2. 作为每个阶段 Gate 的“脚本化”检查清单；  
> 3. 帮助 QA 录制演示录像并快速定位问题。

---

## 目录

1. [Ping](#ping)
2. [Reload Config](#reload-config)
3. [Chat Echo](#chat-echo)
4. [Get Player Name](#get-player-name)
5. [LLM Stream Test](#llm-stream-test)
6. [LLM JSON Test](#llm-json-test)
7. [LLM Tools Test](#llm-tools-test)
8. [LLM Batch Test](#llm-batch-test)

---

## Ping

| 属性 | 说明 |
|------|------|
| **按钮标签** | `Ping` |
| **功能** | 简单验证 **DI 容器** 初始化是否成功。点击后输出 `Ping button clicked – DI container state: OK` |
| **调用链** | `Debug UI` → `ServiceContainer` 单例检查 |
| **输出示例** | `Ping button clicked – DI container state: OK` |

### 代码片段
```csharp
// 简单输出，验证窗口及 DI
AppendOutput("Ping button clicked – DI container state: OK");
```

---

## Reload Config

| 属性 | 说明 |
|------|------|
| **按钮标签** | `Reload Config` |
| **功能** | 调用 `IConfigurationService.Reload()`，动态刷新配置并输出最新 Temperature。 |
| **调用链** | `Debug UI` → `IConfigurationService.Reload` → `OnConfigurationChanged`(事件广播) |
| **输出示例** | `Config Reloaded – New Temperature: 0.65` |

### 代码片段
```csharp
var configSvc = CoreServices.Locator.Get<IConfigurationService>();
configSvc.Reload();
AppendOutput($"Config Reloaded – New Temperature: {configSvc.Current.LLM.Temperature}");
```

---

## Chat Echo

| 属性 | 说明 |
|------|------|
| **按钮标签** | `Chat Echo` |
| **功能** | 发送固定 Prompt `"hello"` 至当前默认 Chat Provider，展示完整回复文本。|
| **是否流式** | 否（`Stream = false`） |
| **调用链** | `Debug UI` → `ILLMService.GetResponseAsync`(Core) → `RimAIApi.GetCompletionAsync`(Framework) → Provider → LLM |
| **输出示例** | `Echo Response: Hello, how can I help you today? | Retries: 0 | CacheHits: 3` |

### 代码片段
```csharp
var response = await llm.GetResponseAsync("hello");
AppendOutput($"Echo Response: {response} | Retries: {llm.LastRetries} | CacheHits: {llm.CacheHits}");
```

### 关键点
* 演示 **非流式** 最小请求。
* 通过修改 Prompt 验证缓存、重试与温度配置是否生效。

---

## Get Player Name

| 属性 | 说明 |
|------|------|
| **按钮标签** | `Get Player Name` |
| **功能** | 使用 `IWorldDataService.GetPlayerNameAsync()` 安全读取玩家派系名称，并测量调用耗时。|
| **调用链** | `Debug UI` → `IWorldDataService.GetPlayerNameAsync`(Core) → `ISchedulerService`(主线程调度) → RimWorld API |
| **输出示例** | `<color=green>Player Faction Name: New Arrivals (Δ 0.42 ms)</color>` |

### 代码片段
```csharp
var name = await world.GetPlayerNameAsync();
var ms   = sw.Elapsed.TotalMilliseconds;
var tag  = ms <= 1.0 ? "color=green" : "color=red";
AppendOutput($"<${tag}>Player Faction Name: {name} (Δ {ms:F2} ms)</${tag}>");
```

### 关键点
* **P3 Gate**：主线程调度不得造成 UI 卡顿；正常情况下 Δ≤1 ms。
* 输出使用富文本颜色标记性能：≤1 ms 绿色；否则红色。

---

## LLM Stream Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM Stream Test` |
| **功能** | 以 **Server-Sent Events** 方式流式获取模型回答，实时打印 `ContentDelta`。|
| **Prompt** | `"你好，简单介绍下Rimworld这个游戏，一句话，尽量简短。"` |
| **是否流式** | 是（`Stream = true`） |
| **调用链** | `Debug UI` → `ILLMService.StreamResponseAsync`(Core) → `RimAIApi.StreamCompletionAsync`(Framework) → Provider → LLM |
| **输出示例** |
```
Start Streaming...
RimWorld 是一款由 Tynan Sylvester 开发的 ...
Δ 管理殖民者、经营基地并应对随机事件。
[FINISH: stop]
```

### 代码片段
```csharp
var req = new UnifiedChatRequest {
    Stream = true,
    Messages = new() { new ChatMessage { Role = "user", Content = "你好，简单介绍下Rimworld这个游戏，一句话，尽量简短。" } }
};
await foreach (var chunk in llm.StreamResponseAsync(req)) {
    if (chunk.IsSuccess && !string.IsNullOrEmpty(chunk.Value?.ContentDelta))
        AppendOutput(chunk.Value.ContentDelta);
    if (!string.IsNullOrEmpty(chunk.Value?.FinishReason))
        AppendOutput($"[FINISH: {chunk.Value.FinishReason}]");
}
```

### 关键点
* **增量输出** 依赖 `UnifiedChatChunk.ContentDelta`。
* 适合验证 SSE 解析与 UI 实时刷新。

---

## LLM JSON Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM JSON Test` |
| **功能** | 通过 `ForceJsonOutput = true` 请求模型以 **JSON 格式** 返回电商产品信息。|
| **Prompt** | `"请用 JSON 格式返回一个测试用的电商产品信息，包含产品名称、价格、描述、图片URL。"` |
| **是否流式** | 否 |
| **调用链** | `Debug UI` → `RimAIApi.GetCompletionAsync`(Framework) |
| **输出示例** |
```json
{"name":"测试键盘","price":199.0,"description":"机械键盘…","image":"https://example.com/kb.jpg"}
```

### 代码片段
```csharp
var jreq = new UnifiedChatRequest {
    ForceJsonOutput = true,
    Stream = false,
    Messages = new() {
        new ChatMessage {
            Role = "user",
            Content = "请用 JSON 格式返回一个测试用的电商产品信息，包含产品名称、价格、描述、图片URL。"
        }
    }
};
var jres = await RimAIApi.GetCompletionAsync(jreq);
```

### 关键点
* `BuiltInTemplates.OpenAiJsonMode` 会自动插入 `"response_format": {"type":"json_object"}` 参数。  
* 返回字符串需自行反序列化与验证。

---

## LLM Tools Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM Tools Test` |
| **功能** | 声明函数工具 `sum_range(start,end)`，模型调用工具后，本地执行并将结果回传，再请模型生成最终自然语言回复。|
| **是否流式** | 否 |
| **调用链** | `Debug UI` → `RimAIApi.GetCompletionAsync` → 模型返回 *tool_calls* → 本地执行 → `RimAIApi.GetCompletionAsync`(第二次) |
| **工具定义** |
```json
{
  "name": "sum_range",
  "description": "Calculate the sum of integers from start to end (inclusive)",
  "parameters": {
    "type": "object",
    "properties": {
      "start": {"type":"integer"},
      "end":   {"type":"integer"}
    },
    "required": ["start","end"]
  }
}
```

| **Prompt** | `"请使用 sum_range 工具计算 1 到 100 的和。"` |
| **输出示例** |
```json
{"tool_calls":[{"name":"sum_range","arguments":{"start":1,"end":100}}]}
...
5050
```

### 代码片段（摘录）
```csharp
var toolDef = new ToolDefinition {
    Type = "function",   // ⚠️ 必须显式声明为 function
    Function = functionObj
};
var req = new UnifiedChatRequest {
    Messages = new() { new ChatMessage { Role = "user", Content = prompt } },
    Tools    = new() { toolDef }
};
var res1 = await RimAIApi.GetCompletionAsync(req);
// 解析 tool_calls → 本地计算 sum → follow-up 请求
```

### 关键点
* 若模型未返回 `tool_calls`，应视为错误并提示。  
* follow-up 请求须携带 **同一 tools 数组**，并按 OpenAI 规范插入三条消息：user / assistant (tool_calls) / tool（结果）。

---

## LLM Batch Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM Batch Test` |
| **功能** | 并发向模型发送 **5** 条不同语言的问候，并展示各自回复。|
| **Prompts** | `"Hello!", "你好！", "¡Hola!", "Bonjour!", "こんにちは！"` |
| **是否流式** | 否（批量异步并发） |
| **调用链** | `Debug UI` → `RimAIApi.GetCompletionsAsync`(Framework) → `ChatManager.ProcessBatchRequestAsync`(内部并发控制) |
| **输出示例** |
```
Batch requesting...
[0] Hello! -> Hello there! How can I assist you?
[1] 你好！ -> 你好！有什么可以帮您？
[2] ¡Hola! -> ¡Hola! ¿En qué puedo ayudarte?
[3] Bonjour! -> Bonjour ! Comment puis-je vous aider ?
[4] こんにちは！ -> こんにちは！ご用件は何でしょうか？
```

### 代码片段
```csharp
var prompts = new[]{"Hello!","你好！","¡Hola!","Bonjour!","こんにちは！"};
var requests = prompts.Select(p => new UnifiedChatRequest {
    Stream = false,
    Messages = new() { new ChatMessage { Role = "user", Content = p } }
}).ToList();
var results = await RimAIApi.GetCompletionsAsync(requests);
```

### 关键点
* `ProcessBatchRequestAsync` 内部使用 **信号量** 控制并发，默认上限 5，可通过模板参数 `concurrencyLimit` 覆写。

---

### 结语

以上 **8** 个示例全面覆盖了 **DI 验证、配置热重载、主线程防腐 & 性能测试、非流式 / 流式聊天、JSON 强制格式、函数工具调用、批量并发** 场景，足以作为 RimAI Core ↔ Framework 的端到端链路验证脚本。 开发者在调试或扩展功能时，可参考本文件快速定位问题或编写新的测试案例。