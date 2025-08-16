# RimAI V5 — P10 实施计划（信息传输 UI：ChatWindow）

> 目标：交付一个用于“玩家 ↔ 小人”对话的独立 UI——`ChatWindow`。该 UI 仅在 UI 层启用流式展示（符合 V5 全局“后台非流式、仅 UI/Debug 允许流式”的纪律），包含左侧信息卡与右侧四行瀑布式布局（标题、聊天主体、指示灯、输入行），并提供快捷键、取消流式、最终输出写入历史等完整闭环。本文档为唯一入口，无需翻阅其他文件即可完成落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（`docs/V5_GLOBAL_CONVENTIONS.md`）与《V5 架构文档》（`docs/ARCHITECTURE_V5.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 新增 UI 子系统 `Source/UI/ChatWindow`：独立业务窗口，不依赖 Stage/Orchestration 生命周期。
  - 右列聊天主体“必须且仅有流式输出”（UI 流式）：
    - 闲聊：使用 `ILLMService.StreamResponseAsync(...)` 真流式展示。
    - 命令：后台非流式拿到最终结果，在 UI 端“伪流式”切片渲染。
  - 左列信息卡：小人头像/信息 + 三个子页签按钮（历史记录 / 人格信息 / 职务管理）。
  - 右列四行布局：标题、聊天主体（斑马栅格/圆弧边框+阴影）、指示灯（Data/Busy/Fin + 右侧 LCD 跑马灯）、输入行（多行富文本 + 三按钮 + 快捷键）。
  - 历史写入：仅在“最终输出”阶段写入（玩家输入 + AI 最终文本），不落中间 chunk。

- 非目标（后续阶段/其他模块处理）
  - 不扩展 LLM/Tooling/Orchestration 的内部契约；命令路径只消费现有服务产出。
  - 不在本阶段交付左侧三个子页签的完整业务细节（放入后续 P10.x 或 P7/P8 各自里程碑）。
  - 不直接做文件 IO 或 Scribe 存档（遵循 P6 边界）。

---

## 1. 架构总览（全局视角）

- 依赖关系（仅消费既有服务，保持最小耦合）
  - `ILLMService`（P2）：闲聊流式；命令可使用非流式获取最终文本。
  - `IOrchestrationService`（P5，可选）：命令路径若需工具执行，编排非流式返回结构化结果 → UI 负责渲染。
  - `IHistoryService`（P8）：仅写入“最终输出”（玩家输入 + AI 最终文本）。
  - `IPersonaService / IPersonaJobService`（P7，可选）：显示人格/职务（只读快照）。
  - `IWorldDataService`（P3，只读）：读取头像/名称等展示所需信息（经主线程调度）。
  - `IConfigurationService`（P1）：读取 UI 配置（主题/快捷键开关/伪流式节奏等）。

- 不变式（与 V5 对齐）
  - 访问边界：Framework 仍仅在 `LLMService`；UI 不直接 `using RimAI.Framework.*`。
  - 非流式纪律：后台/服务路径一律非流式；仅本 ChatWindow UI 与 Debug 窗口启用流式展示。
  - 历史域（P8）：仅保存最终输出；不保存中间 chunk。
  - 日志：统一前缀 `[RimAI.Core][P10]`，避免敏感正文。
  - 系统提示词与输入前缀（ChatUI）：
    - System Prompt：`你是一个Rimworld边缘世界的新领地殖民者，你的总督正在通过随身通信终端与你取得联系。`
    - 用户输入在送入 LLM/编排前增加前缀：`总督传来的信息如下：`

- 生命周期
  - 打开窗口 → 绑定小人（participantId=`pawn:<loadId>`）与玩家（`player:<saveInstanceId>`）→ 生成 `convKey = join('|', sort(participantIds))`。
  - 发送（闲聊/命令）→ 流式/非流式消费 → 指示灯反馈 → 最终输出写入历史。

---

## 2. 目录结构与文件

```
RimAI.Core/
  Source/
    UI/
      ChatWindow/
        ChatWindow.cs                // 主窗口：两列大布局、右列四行容器、事件分发
        ChatController.cs            // 控制器：发送/流式/取消/历史写入/状态机
        ChatModels.cs                // 模型：消息/状态/指示灯/子页签枚举
        Parts/
          LeftSidebarCard.cs         // 左列信息卡 + 三个子页签按钮
          TitleBar.cs                // 标题行（头像 + 名称 + 职务）
          ChatTranscriptView.cs      // 主体（圆角边框、斑马栅格、滚动、名称+时间戳）
          IndicatorLights.cs         // 指示灯行（Data/Busy/Fin，带阴影；节流闪烁/常亮）
          InputRow.cs                // 输入行（富文本 + 闲聊/命令/中断传输 + 快捷键）
        Strings/
          Keys.cs                    // 文案/本地化键
```

入口接入点（强制）：
- 为“小人”新增一个“菜单常规按钮（Gizmo）”，按钮文案为“信息传输”，与 Debug 同级显示；不使用 Debug 按钮作为入口。

---

## 3. 接口契约与模型（UI 内部 API）

> 以下为 UI 层内部契约，保持对外最小面；不新增 `RimAI.Core.Contracts` 对外接口。

```csharp
// ChatModels.cs（要点）
internal enum ChatTab { History, Persona, Job }          // 左列三个子页签
internal enum MessageSender { User, Ai }

internal sealed class ChatMessage {
  public string Id { get; init; }
  public MessageSender Sender { get; init; }
  public string DisplayName { get; init; }               // 展示名
  public DateTime TimestampUtc { get; init; }
  public string Text { get; set; }                       // 流式过程中可增量拼接
  public bool IsCommand { get; init; }
}

internal sealed class IndicatorLightsState {
  public bool DataOn { get; set; }
  public bool FinishOn { get; set; }
  public DateTime DataBlinkUntilUtc { get; set; }
}

internal sealed class ChatConversationState {
  public string ConvKey { get; init; }                   // join('|', sort(participantIds))
  public IReadOnlyList<string> ParticipantIds { get; init; }
  public List<ChatMessage> Messages { get; } = new List<ChatMessage>();
  public IndicatorLightsState Indicators { get; } = new IndicatorLightsState();
}
```

```csharp
// ChatController.cs（要点）
internal sealed class ChatController {
  public ChatConversationState State { get; }

  public Task StartAsync(/* pawnLoadId, playerSaveInstanceId, ... */);
  public Task SendSmalltalkAsync(string text, CancellationToken ct = default);  // 闲聊：真流式
  public Task SendCommandAsync(string text, CancellationToken ct = default);    // 命令：后台非流式 + UI 伪流式
  public void CancelStreaming();                                               // 取消（闲聊/命令通用）

  // UI 消费：OnGUI 每帧调用，出队已到达的 chunk 并更新最后一条 AI 消息文本
  public bool TryDequeueChunk(out string chunk);
}
```

设计说明：
- 闲聊路径：`ILLMService.StreamResponseAsync` → 逐 chunk 入队；完成时 `FinishOn=true`，写入历史。
- 命令路径：`IOrchestrationService.ExecuteAsync` 或其他非流式调用获得完整文本 → UI 端按固定节奏切片“伪流式”入队；完成时写入历史。
- 历史写入：仅在完成阶段将“玩家输入 + AI 最终文本”写入 `IHistoryService`；不保存中间 chunk。
- 线程模型：网络/服务调用均异步；OnGUI 主线程每帧消费 `ConcurrentQueue<string>` 更新文本；禁止 `.Wait()`/`.Result`。

---

## 4. 布局与绘制规范（像素/比例建议）

- 大布局：左右两列，比例 1:5（左列≈1/6 窗口宽；右列≈5/6）。
- 左列（信息卡）
  - 头像（方/圆形裁切）、名称、基础信息、当前职务（未设置显示“无职务”）。
  - 三个按钮切换 `ChatTab`：历史记录 / 人格信息 / 职务管理（本阶段可占位）。
- 右列四行（瀑布布局，高度权重建议 1:7:1:3，可按 UI 观感微调）
  1) 标题行：半身像头像 + 名称 — 职务同一行（未设置→“无职务”）；右侧显示“生命体征”标题与健康脉冲小窗口。
  2) 聊天主体：
     - Chatbot 风格、圆弧边框 + 阴影外框；滚动区域自下向上或自上向下均可，但需保持新增时可见。
     - 玩家与 AI 发言统一靠左；行内包含“名称 + 时间戳”。
     - 分隔样式：玩家=深蓝灰底、AI=无底色；行间距与内边距统一。
     - 滚动交互：上滑浏览时不强制回底；仅在接近底部且有新内容时自动吸底。
  3) 指示灯行：
     - 左红色 `Data`：每收一个 chunk 短暂点亮（带轻微阴影），无 chunk 则暗。
     - 中黄色 `Busy`：流式进行中常亮；结束时熄灭。
     - 右绿色 `Fin`：流式完成后常亮，新一轮开始时熄灭。
     - 右侧：LCD 跑马灯采用“随机队列循环拼接”，同一会话使用稳定种子；单空格分隔；完整滚动一轮后再刷新。
  4) 输入行：
     - 多行富文本框（支持颜色/粗体）；按钮：`闲聊发送`、`命令发送`、`中断传输`。
     - 快捷键：`Shift+Enter` → 闲聊发送；`Ctrl+Enter` → 命令发送；常规 `Enter` 插入换行。
     - 流式进行时：禁用两个“发送”按钮与对应快捷键，仅保留“中断传输”。当小人死亡：同样禁用“闲聊发送/命令发送”。

---

## 5. 线程与流式策略（必须遵守）

- 真流式（仅限 UI/Debug）：
  - 闲聊：调用 `ILLMService.StreamResponseAsync` 获取 `IAsyncEnumerable<string>`/等价事件；后台 Task 读取并入队；UI OnGUI 每帧 `TryDequeueChunk` 消费。
  - 指示灯：每消费一个 chunk → `DataOn=true` 且 `DataBlinkUntilUtc = now + 200ms`；超时后自动熄灭；完成时 `FinishOn=true`。
  - 兼容取消：中断时失效 Framework 会话缓存，并以会话自增编号屏蔽旧流延迟包。

- 伪流式（命令路径）：
  - 后台非流式拿到最终文本；UI 端按固定窗口大小（如 24–48 字）与节奏（30–50ms）切片入队，模拟流动视觉。
  - 送入编排前为用户输入增加前缀：`总督传来的信息如下：`。

- 取消：
  - 使用 `CancellationTokenSource`；点击“中断传输”或关闭窗口 → 触发 `Cancel()` 并复位指示灯与队列。
  - 同时执行：删除半成品 AI、撤回最新用户消息并回填输入框、清空剩余 chunk、失效会话缓存。

- 主线程守则：
  - 任何 Verse 访问（头像/名称等）必须经 `ISchedulerService` 主线程化；UI 不阻塞主线程，不 `.Wait()`/`.Result`。

---

## 6. 配置（内部 CoreConfig.UI.ChatWindow 建议）

> 通过 `IConfigurationService` 读取；不新增对外 Snapshot 字段，保持对外最小面。

```json
{
  "UI": {
    "ChatWindow": {
      "Enabled": true,
      "Zebra": { "User": "#F7F7F7", "Ai": "#EEF6FF" },
      "BorderRadius": 6,
      "PseudoStream": { "ChunkChars": 36, "IntervalMs": 40 },
      "Indicator": { "BlinkMs": 200 },
      "MaxMessages": 200,
      "Hotkeys": { "Smalltalk": "Shift+Enter", "Command": "Ctrl+Enter" }
    }
  }
}
```

说明：
- `PseudoStream` 仅影响命令路径的 UI 切片节奏，不改变后台非流式原则。
- 颜色仅为建议；在 RimWorld 皮肤下可适配游戏内主题。

---

## 7. 实施步骤（一步到位）

> 按顺序完成 S1→S12，可边开发边自检。涉及日志统一使用前缀 `[RimAI.Core][P10]`。

### S1：建立目录与骨架
- 新建 `Source/UI/ChatWindow/**` 目录与 5 个部件文件；`ChatWindow.cs` 挂载四行容器。

### S2：模型与契约
- 实现 `ChatModels.cs`：`ChatMessage`/`ChatConversationState`/`IndicatorLightsState`/`ChatTab` 等。

### S3：控制器（ChatController）
- 依赖注入：构造函数注入 `ILLMService`、`IHistoryService`、`IWorldDataService`、`IConfigurationService`、（可选）`IOrchestrationService`、`IPersonaService/JobService`。
- 实现：闲聊真流式、命令非流式 + 伪流式、取消、历史写入、线程安全队列。

### S4：左列信息卡（LeftSidebarCard）
- 绘制头像/名称/职务（未设置→“无职务”）；按钮切换三个子页签（本阶段占位内容）。

### S5：标题行（TitleBar）
- 半身像头像（裁切/放大，轻微下移）+ 名称 — 职务同一行显示；适配长名称截断；提供 Tooltip。
- 右侧新增“生命体征”小窗口与标题，显示健康脉冲（见 S5.1）。

### S5.1：生命体征（Vital Signs）
- 在标题栏右侧放置 EKG 风格“健康脉冲”小窗口：
  - 颜色阈值：>80% 绿色；>40% 黄色；≤40% 红色；死亡=灰色水平线。
  - 随机性：每个心跳周期的峰/谷倍率与基线偏移为固定随机（同周期内不变，跨周期变化）。
  - 数据获取（P3 合规）：调用 `IWorldDataService.GetPawnHealthSnapshotAsync(pawnLoadId)` 获取 10 项能力（0..1）与 `IsDead`；UI 可直接计算均值（0..100）。
  - 工具化：提供 `get_pawn_health` 工具用于外部调用（工具执行层可完成均值计算）。
  - 行为：当 `IsDead=true` 时，禁用“闲聊发送/命令发送”与对应快捷键（仅保留“中断传输”）。

### S6：聊天主体（ChatTranscriptView）
- 圆弧边框容器 + 滚动区域；名称 + 时间戳；玩家/AI 左对齐；斑马栅格；自动滚动到底。

### S7：指示灯行（IndicatorLights）
- 三个矩形小灯（红=Data、黄=Busy、绿=Fin）带轻阴影；根据 `IndicatorLightsState` 与流式状态渲染；Data 节流闪烁、Busy 表示进行中、Fin 表示完成。
- 右侧 LCD 跑马灯：文本采用“随机队列循环拼接”，同一会话用稳定种子；单空格间隔；等一轮完整滚动后再刷新。

### S8：输入行（InputRow）
- 多行富文本框（黑底圆角）；按钮：`闲聊发送`、`命令发送`、`中断传输`；快捷键处理（焦点在文本框时捕获 `Shift+Enter`/`Ctrl+Enter`）。
- 流式进行时：禁用两个“发送”按钮与对应快捷键，仅保留“中断传输”。

### S9：窗口整合（ChatWindow.cs）
- 两列 1:5 布局；右列四行；窗口打开/关闭生命周期；与 `ChatController` 连接；OnGUI 每帧消费 chunk/刷新指示灯。
- 指示灯行右侧增加“LCD 跑马灯”（黑底、绿色像素；流式进行中切换为黄色像素），按秒级整步进滚动（一次步进 3 个 LED 列），无缝循环，避免右侧黑缝。

### S10：命令路径（可选集成 Orchestration）
- 将“命令发送”接入 `IOrchestrationService.ExecuteAsync`（非流式），取得结构化结果 → UI 负责渲染文本并伪流式切片。传入的用户文本增加前缀：`总督传来的信息如下：`。

### S11：历史写入
- 闲聊与命令完成后，将“玩家输入 + AI 最终文本”写入 `IHistoryService`；不保存中间 chunk；失败可重试或给出提示。

### S12：入口与本地化（小人菜单常规按钮：信息传输）
- 在选择小人时，于其 Gizmo 菜单新增常规按钮：`信息传输`（非 Debug、常规可见）。
- 建议实现方式：在 `Boot/HarmonyPatcher.cs` 上集中注册 Harmony Patch，向 `Pawn.GetGizmos()`（或等效生成 Gizmo 的方法）注入一个 `Command_Action`：
  - `defaultLabel = "信息传输"`（文案来源于 `Strings/Keys.cs`）。
  - `icon = Textures/ChatWindow/Chat`（若无可使用通用聊天图标或占位）。
  - `action = () => WindowStack.Add(new ChatWindow(selectedPawn))`。
  - 仅在 `UI.ChatWindow.Enabled=true` 且选择目标为 `Pawn` 时显示。
  - 与 Debug 同级（常规 Gizmo 列表中），不依赖开发者模式。
- 本地化键新增：`Keys.UI.ChatWindow.Open = "信息传输"`。

---

## 8. 验收 Gate（必须全绿）

- 布局与交互
  - 左右两列比例 1:5；左列信息卡 + 三按钮；右列四行完整可用。
  - 标题行展示半身像头像/名称 — 职务同一行（未设置→“无职务”）。
  - 聊天主体：玩家/AI 左对齐、名称+时间戳；玩家为深蓝灰底、AI 无底色；滚动稳定（上滑不回底、接近底部新增自动吸底）。
  - 指示灯：chunk 到达时 Data 闪烁；进行中 Busy=黄；完成后 Fin 常亮；新一轮开始时 Fin 熄灭。
  - 生命体征：右上方显示 EKG 波形小窗，含左侧标题“生命体征”；颜色阈值与死亡灰线符合规范；每个心跳周期形状固定、跨周期随机。
  - LCD 跑马灯：文本采用随机队列循环拼接；单空格间隔；完整滚动一轮后再刷新。
  - 输入行：多行富文本；快捷键生效：`Shift+Enter`（闲聊）、`Ctrl+Enter`（命令）；取消可中断。
  - 流式期间禁用两个“发送”按钮与快捷键，仅保留“中断传输”；中断后用户文本回填输入框，不保留半成品 AI 消息。

- 纪律与边界
  - 流式仅在 `Source/UI/ChatWindow/**`（与 Debug 面板）出现：仓级检查 `StreamResponseAsync\(` 在后台目录计数=0。
  - UI 不直接 `using RimAI.Framework.*`；Framework 面仅在 `Modules/LLM/**` 出现。
  - 历史写入仅保存最终输出；不保存 chunk。
  - 依赖注入仅“构造函数注入”；项目内检查禁止属性注入与 Service Locator。

- 日志与性能
  - 日志统一前缀 `[RimAI.Core][P10]`；避免输出敏感正文。
  - 正常对话过程中，每帧新增 ≤ 1ms；流式更新不卡顿；取消后 200ms 内复位指示灯。

---

## 9. 回归脚本（人工/录屏）

1) 选择某个小人 → 点击小人菜单常规按钮“信息传输”打开 `ChatWindow`。
2) 输入 2 段文字：
   - 闲聊：按 `Shift+Enter` 发送 → 观察流式逐字出现、Data 闪烁、Busy 常亮、完成后 Fin 常亮。
   - 命令：按 `Ctrl+Enter` 发送 → 观察 UI 伪流式切片；完成后 Fin 常亮。
3) 点击“中断传输”中断进行中的会话 → chunk 停止、Data 熄灭、Busy 熄灭、Fin 不亮；用户输入回填到输入框；无半成品 AI 残留。
4) 切换左列三个子页签无异常；标题行信息正确，未设职务显示“无职务”。
5) 关闭再打开窗口，历史中可看到玩家输入与 AI 最终输出（不含中间 chunk）。

---

## 10. CI / Gate（使用 Cursor 内置工具，必须通过）

- 流式纪律（强制）：
  - 检查=0：后台/服务路径 `StreamResponseAsync\(`。
  - 允许匹配：`Source/UI/ChatWindow/**` 与 `Source/UI/DebugPanel/**`。

- 访问边界：
  - 检查=0：除 `Modules/LLM/**` 外 `using\s+RimAI\.Framework`。
  - 检查=0：除 `Modules/World/**`、`Modules/Persistence/**` 外 `using\s+Verse|\bScribe\.`。
  - 检查=0：除 `Modules/Persistence/**` 外 `using\s+System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b`。

- 注入纪律：
  - 检查=0：属性注入与 Service Locator 约定模式。

- 日志前缀（建议性 Gate）：
  - 抽样 `Log\.(Message|Warning|Error)\(` 调用文本以 `[RimAI.Core]` 开头。

---

## 11. 性能与可靠性预算

- 启动渲染 ≤ 50ms；打开窗口 ≤ 30ms；首个流式 chunk ≤ 2s（受上游网络影响）。
- OnGUI 消费 chunk 不得造成 GC 峰值；建议复用 StringBuilder/样式缓存。
- 取消响应 ≤ 100ms；指示灯闪烁周期默认 200ms（可配置）。

---

## 12. 安全与隐私

- 日志仅记录会话 ID 哈希/参与者摘要，不输出敏感正文。
- 历史域仅保存最终输出与必要元数据；不保存中间 chunk。

---

## 13. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 流式阻塞或首包过慢 | UI 端加载骨架与占位，展示“连接中/心跳”提示；设置首包超时/重试（由 P2 保障）。 |
| 大文本渲染卡顿 | UI 分片渲染；限制 `MaxMessages`；滚动区域虚拟化（后续优化）。 |
| 命令路径“伪流式”与真实进度不一致 | 明确“命令为 UI 伪流式展示”，仅用于观感；完成后一次性落历史。 |
| 取消后残留 chunk | 取消时丢弃队列并复位指示灯；后台协程尊重 `CancellationToken`。 |

---

## 14. FAQ（常见问题）

- Q：为何闲聊使用真流式，命令使用伪流式？
  - A：全局纪律要求后台非流式；命令通常走编排/工具执行链，返回最终结构，因此在 UI 层用伪流式满足观感，不破坏后端非流式原则。

- Q：历史是否保存中间 chunk？
  - A：不保存。P8 规范仅保存“最终输出”，避免冗余与存档膨胀。

- Q：如何接入不同的入口（非 Debug 环境）？
  - A：为 Pawn 添加 Gizmo（或右键菜单）打开 `ChatWindow`；Debug 面板保留测试入口以便验收与回归。

- Q：目标框架与构建要求？
  - A：保持 .NET 4.7.2 兼容；遵循项目现有构建与打包流程。

- Q：ChatUI 提示词与用户输入如何组织？
  - A：系统提示词固定为“你是一个Rimworld边缘世界的新领地殖民者，你的总督正在通过随身通信终端与你取得联系。”；送入模型/编排前为用户输入增加前缀“总督传来的信息如下：”。

---

## 15. 变更记录

- v5-P10（初版）：新增 `ChatWindow` 信息传输 UI；真流式（闲聊）+ 伪流式（命令）；指示灯与快捷键；最终输出写入历史；CI/Gate 对齐 V5 纪律。
- v5-P10（更新）：
  - 指示灯行扩展为 `Data/Busy/Fin`；Busy 表示进行中；Fin 完成亮，新一轮复位。
  - 输入行“取消”改为“中断传输”；流式期间禁用两个“发送”按钮与快捷键；中断回滚用户与半成品 AI；回填文本。
  - ChatUI 提示词与输入前缀落地：见“不变式”与“命令路径”条目。
  - LCD 跑马灯：黑底、绿色像素（流式切黄）、按秒整步进 3 列、无缝循环。
  - 可靠性：加入会话自增编号与会话缓存失效，避免中断后的尾包/缓存干扰。

---

本文为 V5 P10 唯一权威实施说明。实现与验收以本文为准。


