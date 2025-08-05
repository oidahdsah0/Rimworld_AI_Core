# RimAI Debug 面板调用示例

> 文档版本：P2（对应 `MainTabWindow_RimAIDebug.cs` 最近提交）。
>
> 该文件汇总了调试面板内 **5** 个按钮的完整调用链、请求结构与期望输出。
> 便于其他开发者理解 Core ↔ Framework ↔ LLM 三层之间的交互细节。

---

## 目录

1. [Chat Echo](#chat-echo)
2. [LLM Stream Test](#llm-stream-test)
3. [LLM JSON Test](#llm-json-test)
4. [LLM Tools Test](#llm-tools-test)
5. [LLM Batch Test](#llm-batch-test)

---

## Chat Echo

| 属性 | 说明 |
|------|------|
| **按钮标签** | `Chat Echo` |
| **功能** | 发送一句固定 Prompt(`"hello"`) 至当前默认 Chat Provider，展示完整回复文本。|
| **是否流式** | 否（`Stream = false`） |
| **调用链** | `Debug UI` → `ILLMService.GetResponseAsync`(Core) → `RimAIApi.GetCompletionAsync`(Framework) → `ChatManager` → Provider → LLM |
| **输出示例** | `Echo Response: Hello, how can I help you today? \| Retries: 0 \| CacheHits: 3` |

### 代码片段
```csharp
var response = await llm.GetResponseAsync("hello");
AppendOutput($"Echo Response: {response} | Retries: {llm.LastRetries} | CacheHits: {llm.CacheHits}");
```

### 关键点
* 演示 **非流式** 最小请求。
* 可通过修改 Prompt 验证缓存、重试与温度配置是否生效。

---

## LLM Stream Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM Stream Test` |
| **功能** | 以 **Server-Sent Events** 方式流式获取模型回答，实时打印 `ContentDelta`。|
| **Prompt** | `"你好，简单介绍下 Rimworld 这个游戏。"` |
| **是否流式** | 是（`Stream = true`） |
| **调用链** | `Debug UI` → `ILLMService.StreamResponseAsync` → `RimAIApi.StreamCompletionAsync` → `ChatManager.TranslateStreamAsync` → Provider → LLM |
| **输出示例** |
```
Start Streaming...
RimWorld 是一款由 Tynan Sylvester 开发的 ...
Δ 开始游戏时，玩家将扮演殖民者...
...
[FINISH: stop]
```

### 代码片段
```csharp
var req = new UnifiedChatRequest {
    Stream = true,
    Messages = new List<ChatMessage> {
        new ChatMessage { Role = "user", Content = "你好，简单介绍下 Rimworld 这个游戏。" }
    }
};
await foreach (var chunk in llm.StreamResponseAsync(req)) {
    if (chunk.IsSuccess && !string.IsNullOrEmpty(chunk.Value?.ContentDelta))
        AppendOutput(chunk.Value.ContentDelta);
}
```

### 关键点
* **增量输出** 依赖模板 `ResponsePaths.Content`，已于 v4.4 修正为 `content`，否则为空。
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
    Messages = new List<ChatMessage> {
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
| **功能** | 向模型声明 **函数工具** `sum_range(start,end)`，要求其调用工具以计算 `1 + … + 100 = 5050`。|
| **是否流式** | 否 |
| **调用链** | `Debug UI` → `RimAIApi.GetCompletionAsync` → 工具调用 JSON → (可选) 本地解析 & 再响应 |
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
```
*若在 Core 侧真正执行工具，再向模型回传，可得到：*
```
5050
```

### 关键点
* RimAI Framework 会把 `Tools` 数组翻译为 OpenAI compatible `tools` 字段。  
* 解析 `tool_calls` 并执行需开发者在 Core 侧自行实现。

---

## LLM Batch Test

| 属性 | 说明 |
|------|------|
| **按钮标签** | `LLM Batch Test` |
| **功能** | 并发向模型发送 **5** 条不同语言的问候，并展示各自回复。|
| **Prompts** |
`"Hello!", "你好！", "¡Hola!", "Bonjour!", "こんにちは！"` |
| **是否流式** | 否（批量异步并发） |
| **调用链** | `RimAIApi.GetCompletionsAsync` → `ChatManager.ProcessBatchRequestAsync`(|并发控制|) |
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
    Messages = new List<ChatMessage> { new ChatMessage { Role="user", Content = p } }
}).ToList();
var results = await RimAIApi.GetCompletionsAsync(requests);
```

### 关键点
* `ProcessBatchRequestAsync` 内部使用 **信号量** 控制并发，默认上限 5，可在模板里通过 `concurrencyLimit` 覆写。

---

### 结语

以上五个示例覆盖了 **非流式、流式、JSON 强制格式、工具调用、批量并发** 五种常见场景，足以验证 RimAI Core ↔ Framework 的端到端链路。

开发者可参考此文档快速定位问题或二次开发。