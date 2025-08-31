# V5 P16 — 服务器窗体（ServerChatWindow）从零实现指导书（面向 .NET/C# 新手）

> 目标读者：没有 C# 基础或刚入门 .NET 的开发者。
>
> 产物目标：在不引入卡顿/卡死的前提下，从零实现“服务器窗体”（面向 AI 服务器的专用对话/管理 UI），严格遵守 V5 全局纪律与边界。
>
> 重要纪律回顾（必须遵守）：
> - Verse（游戏世界对象）访问仅能在 P3：WorldDataService 内，并且必须通过主线程调度器 ISchedulerService 进行主线程化。
> - 后台/服务路径一律非流式（不使用流式输出）。流式只允许在 `Source/UI/ChatWindow/**`，服务器窗体属于“非 ChatWindow UI”，因此禁止流式。
> - 统一使用 `UnifiedChatRequest`（system+历史+本次用户输入），历史域只保存“最终输出”。
> - 文件 IO 统一走 P6 持久化服务；工具 JSON 统一来自 P4 工具服务。
> - 键规范：`participantId` 使用 `thing:<id>`/`player:<id>` 等；`convKey = join('|', sort(participantIds))`；保留 `agent:stage`。

---

## 1. 你要做成什么样（结果图景）

- 一个 RimWorld 的 Window 派生窗体（命名建议：`ServerChatWindow`）。
- 左侧是服务器列表（仅展示内存中的“服务器记录”），右侧是会话区：
  - 顶部标题：当前服务器简要信息（序列号、等级、人格预设短名）。
  - 中部对话区：玩家与“服务器 AI”的往返消息（仅最终文本）。
  - 底部指示灯与输入行：显示忙碌状态、输入小聊/命令、取消按钮。
  - 页签切换：历史面板（异步加载）、人格编辑器、跨服务器通讯（异步加载）。
- 温度小图：右上角小区域定期刷新（后台轮询），不阻塞 UI。
- 全链非流式；不会卡 UI 帧；不会直接触达 Verse；不使用 `.Wait()`/`.Result`/`GetResult()`。

---

## 2. 目录与文件清单（建议结构）

在 `RimAI.Core/Source/UI/ServerChatWindow/` 目录下创建：

- `ServerChatWindow.cs` — 顶层窗体：布局、事件路由。
- `ServerChatController.cs` — 控制器：业务编排与状态管理（加载历史、发送消息/命令、取消）。
- `ServerChatModels.cs` — 会话状态/消息/枚举（POCO 类型）。
- `Parts/ServerListSidebar.cs` — 左侧服务器列表与导航按钮。
- `Parts/ServerConversationHeader.cs` — 顶部标题渲染（只读展示）。
- `Parts/ServerTemperatureChart.cs` — 温度迷你图（仅绘制已有采样）。
- `Parts/ServerHistoryTabs.cs` — 历史页签（异步加载/分页，UI 不等待）。
- `Parts/ServerPersonaEditor.cs` — 服务器人格编辑器（读写 IServerService）。
- `Parts/InterServerCommsPanel.cs` — 跨服务器通讯记录（异步加载，UI 不等待）。
- `Strings/Keys.cs` — 本地化键集中，避免硬编码字符串。

说明：你可以按需精简 Parts，但请保留“侧边栏/标题/对话/输入/指示灯”五件套。

---

## 3. 依赖服务（你需要用到谁）

通过容器解析（`RimAICoreMod.Container.Resolve<T>()`）：

- `IServerService`（P13）：服务器基础信息/巡检/提示词打包。
- `IHistoryService`（P8）：读取/写入历史（只保存“最终输出”）。
- `IPromptService`（P11）：构造系统提示与用户前缀（非流式）。
- `ILLMService`（P2）：非流式聊天响应（`GetResponseAsync(UnifiedChatRequest)`）。
- `IOrchestrationService`（P5/P12）：命令模式的“段1 编排与工具执行”。

特别注意：
- Verse 访问（世界数据）不在 UI 或 ServerService 里做，必须走 P3 `IWorldDataService`，且该服务内部已经主线程化了。
- 温度推荐读取使用异步版：`IServerService.GetRecommendedSamplingTemperatureAsync(entityId, ct)`。

---

## 4. 数据与会话“契约”（轻量约定）

- Player 会话 ID：随窗体生命周期生成一个稳定 ID，例如 `player:<8位随机>`（参考：`Guid.NewGuid().ToString("N").Substring(0,8)`）。
- 会话参与者：`[thing:<serverThingId>, player:<id>]` 按字典序排序。
- `convKey = join('|', sort(participantIds))`。
- 历史加载：分页读取最新 N 条，将其转为 `ServerChatMessage`，入队列 `PendingInitMessages`，UI 每帧消费到 `Messages`。
- 写入历史：
  - 用户发送：`advanceTurn=false`（占位/过程，不推进回合）。
  - AI 最终文本：`advanceTurn=true`（推进回合，仅最终输出）。

---

## 5. 交互流程（从点击到回应）

1) 打开窗体（Gizmo 点击）：
- 解析容器，Resolve 所需服务。
- 生成 playerId，计算 convKey，创建 `ServerChatController(State)`。
- `StartAsync()` 后台加载历史（不阻塞 UI）。
- 启动后台温度轮询任务（每 2.5s 调用 `GetRecommendedSamplingTemperatureAsync`，压入 `TemperatureSeries`）。

2) 侧边栏切换服务器：
- 取选中的 `entityId`，重建/更新 controller，触发 `StartAsync()`；
- 标题与会话区随 State 更新；温度轮询读取新 `entityId` 的温度。

3) 发送“小聊”（Smalltalk）：
- UI 线程将输入文本清空并调用 `controller.SendSmalltalkAsync(text)`（fire-and-forget）。
- Controller：
  - 取消既有任务（`Cancel()`），置 Busy。
  - 先把“用户消息”追加到 `State.Messages` 与历史（`advanceTurn=false`）。
  - 异步：向 `IServerService.BuildPromptAsync(...)` 要 Server 侧系统行；同时 `IPromptService.BuildAsync(...)` 生成 ChatUI SystemPrompt；合并为统一 system 文本。
  - 组装 `UnifiedChatRequest`（含 system + 历史消息 + 当前 user），`Stream=false` 调用 `ILLMService.GetResponseAsync`。
  - 返回成功：把 AI 文本写入 `State.Messages`，并 `history.AppendRecordAsync(..., advanceTurn=true)`。
  - 结束：Busy=false。

4) 发送“命令”（Command）：
- 与小聊相同，但在构造 Prompt 前增加“段1 编排”：
  - 调用 `IOrchestrationService.ExecuteAsync`（一次非流式决策+串行工具执行），得到结构化 `ToolCallsResult`。
  - 将其转成简要 JSON 文本作为 `ExternalBlocks` 合并到 Prompt（RAG），再走非流式聊天。
  - 注意：编排阶段的计划追踪（PlanTrace）应写为 AI Note（不推进回合）。

5) 取消：
- `controller.Cancel()` 触发取消令牌，置 Busy=false；丢弃未完成的后台任务。

---

## 6. UI 布局与 Parts 的连接方式

- ServerChatWindow.cs
  - 计算各区域 Rect：左侧栏（列表+导航）、右侧上部标题、中部对话（滚动）、下部指示灯与输入行。
  - 左侧栏调用 `ServerListSidebar.Draw(...)`，传入 `IServerService` 与选择回调。
  - 标题调用 `ServerConversationHeader.Draw(...)`，只读展示当前服务器。
  - 对话 Transcript：每帧将 `State.PendingInitMessages` 出队加入 `State.Messages`，然后绘制；
  - 指示灯：根据 `State.IsBusy` 显示 Busy/Idle 文案；
  - 输入行：三个按钮 → 小聊/命令/取消，分别调用 controller 对应方法（不要在 UI 线程 await）。
  - 页签：History/Persona/InterComms 三个子面板：
    - HistoryTabs 与 InterServerCommsPanel 在首次 Draw 或筛选条件变化时“发起后台异步加载”，并在 UI 显示“Loading…”占位；完成后只读绘制。
    - PersonaEditor 直接读写 `IServerService` 的内存态（Set* 接口）。

- Parts 内禁止：
  - 在 Draw 中使用 `.Wait()`、`.Result`、`GetAwaiter().GetResult()` 等同步等待。
  - 直接访问 Verse 或文件 IO；若需要世界数据或文件，必须由上层服务提供（P3/P6）。

---

## 7. 线程模型（避免卡死的秘诀）

- OnGUI/Draw 是在主线程上执行的，请确保：
  - 不进行任何可能阻塞的操作（网络、LLM、文件、索引、长时间计算）。
  - 所有耗时工作改为 `Task.Run` 或本身的 `async/await`（在后台继续），UI 仅消费结果。
- 访问 Verse 的 API（地图、物品、能力等）只能通过 P3 WorldDataService 提供的异步方法，这些方法内部用 `ISchedulerService.ScheduleOnMainThreadAsync(...)` 主线程化执行。
- 切记不要在主线程调用 `.Wait()`/`.Result`；也不要在后台线程同步调用 Verse API。

---

## 8. 本地化与文案规范

- 所有按钮/标题/标签等文本使用本地化键（例如：`"RimAI.ServerChatUI.Button.Persona"`、`"RimAI.Common.Busy"`）。
- 禁止硬编码中文/英文标点与称谓；若确需拼接，统一从本地化键中取值。
- PlayerTitle 必须通过本地化键回退获取，不允许写死“总督/Governor”等称谓。

---

## 9. 验收与测试（你如何判断做对了）

- 打开“信息传输”Gizmo → 窗体弹出，不出现卡顿或卡死。
- 左侧服务器列表正常显示，切换时右侧标题/会话即时更新。
- 发送小聊/命令：
  - 有用户占位消息，随后返回 AI 最终文本；
  - 历史中仅保存“最终输出”（用户/AI），命令的 PlanTrace 作为 AI Note 不推进回合。
- 温度小图：每约 2.5 秒刷新一次样本，UI 无明显掉帧。
- History/InterComms 页签：首次进入显示“Loading...”，随后填充内容；UI 整体线程稳定。

---

## 10. 常见坑位与解决办法

- 症状：打开窗体立即卡死。
  - 多因在 Draw 中同步等待异步任务（`.Result`/`GetResult()`）；解决：改为后台加载 + UI 占位。
- 症状：随机报 Verse 相关异常（跨线程）。
  - 多因在后台线程直接访问 Verse；解决：所有 Verse 访问改走 P3 WorldData 的异步方法。
- 症状：AI 文本“串包/尾包污染”。
  - 多因未在发送前取消上一个请求；解决：`controller.Cancel()` 先行，建立新的 CancellationTokenSource。
- 症状：历史太多导致滚动卡顿。
  - 采用分页与上限（如 200 条），长文本裁剪；绘制时尽量避免每帧反复测量巨量字符串高度。

---

## 11. 与仓内其他模块的配合关系

- P2 LLM 网关：只用非流式 `GetResponseAsync(UnifiedChatRequest)`；不使用流式 API。
- P3 WorldData：温度、服务器等级、其他世界只读数据，一律通过它。
- P5/P12 Orchestration：命令模式走“一次非流式决策 + 串行工具执行”，产生结构化结果与 PlanTrace。
- P8 历史：仅“最终输出”入档；列表页签需异步拉取，避免 UI 阻塞。
- P11 Prompting：`IPromptService.BuildAsync` 单入口构建系统提示与上下文块；支持 ExternalBlocks 合并。
- P13 Server：`IServerService.BuildPromptAsync` 提供服务器系统行与上下文块，并给出温度建议（异步）。

---

## 12. 质量门禁（你可以自查的标准）

- 目录限制（搜索应为 0 次）：
  - 除 `Source/Modules/LLM/**` 外，检查 `using RimAI.Framework`。
  - 除 `Source/Modules/World/**`、`Source/Modules/Persistence/**` 外，检查 `using Verse|\bScribe\.`。
  - 除 `Source/Modules/Persistence/**` 外，检查 `System.IO|File|Directory|FileStream|StreamReader|StreamWriter`。
  - 后台路径检查：`StreamResponseAsync\(`（服务器窗体内应为 0）。
  - UI/控制器中检查同步等待：`\.Result\b|GetAwaiter\(\)\.GetResult\(|\.Wait\(\)`（应为 0）。
- 日志前缀：建议以 `[RimAI.Core][P13]` 或具体模块前缀记录关键调试日志，避免落敏感正文。

---

## 13. 实施顺序（建议一步一步做）

1) 创建目录与空文件（按第 2 节清单）。
2) 在 `ServerChatModels.cs` 中定义状态/消息/枚举（POCO）。
3) 实现 `ServerChatController`：
   - 构造函数注入服务 + `State`；
   - `StartAsync()` 从历史加载最近 N 条，封装为消息入 Pending 队列；
   - `SendSmalltalkAsync` 与 `SendCommandAsync`（含编排段1），均为非流式；
   - `Cancel()` 取消当前任务。
4) 实现 `ServerChatWindow`：
   - 构造中 Resolve 服务、建立 controller、启动温度轮询；
   - 实现 `DoWindowContents` 布局 + 调用 Parts；
   - 输入行按钮转发到 controller。
5) 实现 Parts：先做 Sidebar/Title/Transcript/Indicators/InputRow，后做 History/Persona/InterComms。
6) 自测 Gate：全文搜索是否存在同步等待与越权访问。
7) 运行游戏内手测：打开、切换、发送、取消、切页签。

---

## 14. 可复用的“最小签名草案”（帮助你对齐形状）

以下是“形状提示”，不是代码模板：

- `ServerChatController`
  - 构造：`(ILLMService, IHistoryService, IPromptService, IServerService, IOrchestrationService, convKey, participantIds, selectedServerEntityId)`
  - `Task StartAsync()`、`Task SendSmalltalkAsync(string, CancellationToken)`、`Task SendCommandAsync(string, CancellationToken)`、`void Cancel()`
  - `static (string convKey, List<string> pids) BuildConvForServer(string serverEntityId, string playerId)`

- `ServerChatConversationState`
  - `string ConvKey`、`IReadOnlyList<string> ParticipantIds`、`List<ServerChatMessage> Messages`、`ConcurrentQueue<ServerChatMessage> PendingInitMessages`
  - `bool IsBusy`、`string SelectedServerEntityId`、`TemperatureSeriesState TemperatureSeries`

- `IServerService`（与本仓当前接口对齐）：
  - `Task<ServerPromptPack> BuildPromptAsync(string entityId, string locale, CancellationToken ct = default)`
  - `Task<float> GetRecommendedSamplingTemperatureAsync(string entityId, CancellationToken ct = default)`
  - `ServerRecord Get(string entityId)`、`IReadOnlyList<ServerRecord> List()`、以及 Persona/巡检相关 Set/Get 方法

---

## 15. FAQ（你可能会问）

- Q：为什么我不能在 UI 里直接 `await`？
  - A：`await` 本身可以用，但不要让 UI 帧“等待一个长任务”。建议使用 fire-and-forget（`_ = Task.Run(async ()=>{...})`）或在逻辑层内部异步处理，UI 只显示状态。

- Q：`GetResult()` 真的不能用吗？
  - A：在主线程（UI/OnGUI）不能用，这会阻塞并导致死锁或卡死。改为异步回调/后台任务。

- Q：我需要读地图/天气/服务器功耗怎么办？
  - A：一律通过 P3 WorldDataService 提供的异步方法获取，该服务会在主线程执行 Verse 访问并返回 POCO 快照。

- Q：能不能让服务器窗体也流式显示？
  - A：不行。V5 纪律中仅 `Source/UI/ChatWindow/**` 允许真流式，服务器窗体属于业务 UI，必须非流式。

---

## 16. 小结

照此文档落地，你将得到：
- 一个从零搭建的“服务器窗体”，不卡 UI，不越权访问 Verse；
- 清晰的控制器/状态/部件分层，易于维护与扩展；
- 与 V5 各阶段（P2/P3/P5/P8/P11/P12/P13）的边界完全对齐。

如需，我可以在下一步自动生成上述空文件与基础骨架代码，并确保通过编译与 Gate 自检，再交付最小可运行版供你在游戏内验证。
