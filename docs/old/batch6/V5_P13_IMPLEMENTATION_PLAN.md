# RimAI V5 — P13 实施计划（Server 服务：基础信息/巡检/提示词）

> 最新注意事项（Debug 页面移除 & 非流式纪律）
>
> - Debug 页面（DebugPanel/Debug Window）已从项目中移除；流式仅允许在 ChatWindow UI 路径展示；后台/服务路径一律非流式。
> - Gate 更新：关于流式 API 的允许路径应视为仅 `Source/UI/ChatWindow/**`；`Source/UI/DebugPanel/**` 不再适用。
> - 所有文件读写一律通过 `IPersistenceService` 提供的统一文件 IO 接口完成；除 `Modules/Persistence/**` 外禁止直接使用 `System.IO` 与 Scribe。

> 版本：v5.0.0-alpha（P13）


## 0. 范围与非目标

- 范围（本阶段交付）
  - 新增“Server 服务”（下称 ServerService）：维护 3 个等级服务器（Lv1/Lv2/Lv3）的基础信息、巡检配置与执行、提示词产物；为 Act 与 Server Chat UI 提供统一提示词输入与上下文块
  - 数据模型与持久化：在 `PersistenceSnapshot` 中新增 `ServerState` 节点；读档/存档/导入/导出全链接入
  - 巡检槽位：按等级提供固定槽位数量；槽位绑定工具（Tool），到期定时执行并汇总文本
  - 环境温度策略：按机房温度映射到 LLM 采样温度（若可用）与系统提示词风格切换
  - 与 Prompt/Act/UI 对接：构建用于 Chat/Act 的 SystemLines 与 ContextBlocks；支持外部块并入 P11 的单入口 `IPromptService.BuildAsync`

- 非目标（后续阶段）
  - 新 UI 面板开发（如“服务器管理器/APPs 管理页”）
  - 自动故障恢复/告警升级策略（仅留扩展点）
  - 复杂多服务器编队/租约仲裁（交由 P9 Stage 承担）
  void SetBaseServerPersonaPreset(string entityId, string presetKey);
  void SetBaseServerPersonaOverride(string entityId, string overrideText);

## 1. 架构总览（与 V5 约束对齐）
  void SetServerPersonaSlot(string entityId, int slotIndex, string presetKey, string overrideText = null); // 范围校验与去重
  void ClearServerPersonaSlot(string entityId, int slotIndex);
  System.Collections.Generic.IReadOnlyList<ServerPersonaSlot> GetServerPersonaSlots(string entityId);

- 访问边界（强约束）
  - Verse：仅 P3 `IWorldDataService` 与 P6 `IPersistenceService` 触达；ServerService 自身不直接 `using Verse`
  - LLM：仅通过 P2 `ILLMService`；后台非流式；UI 真流式仅在 ChatWindow
  - Tool JSON：仍由 P4 `IToolRegistryService` 统一产出；Server 只消费，不自行拼装或向量检索
  - 文件 IO：所有文件读写（模板/预设/配置/导入导出）必须经 `IPersistenceService`（设置文件 JSON）；存档 Scribe 亦由 P6 统一承接

- 与其他模块的关系
  - Server →（读）World：`GetAiServerSnapshotAsync(serverId)` 获取温度/电力等（只读）
  - Server → Tooling：槽位到期串行执行 `ExecuteToolAsync(toolName, args)`；筛选工具按 `MaxToolLevel = server.Level`
  - Server → Prompting：`BuildPromptAsync` 产出 SystemLines/ContextBlocks，供 Act/Chat UI 并入 P11 的 System 段

```mermaid
graph TD
  System.Threading.Tasks.Task<float> GetRecommendedSamplingTemperatureAsync(string entityId, System.Threading.CancellationToken ct = default);
  Act["Acts (P9)"] --> P11
  P11 --> Server["Server Service (P13)"]
  Server --> World["WorldData (P3)"]
  Server --> Tooling["Tooling (P4)"]
  Server --> P6["Persistence (P6)"]
  Server --> P2["ILLMService (P2)"]
```

---

## 2. 接口契约（内部 API）

> 接口位于 `RimAI.Core/Source/Modules/Server/**`，均为 Core 内部 API，不暴露到 Contracts。

```csharp
// RimAI.Core/Source/Modules/Server/IServerService.cs
namespace RimAI.Core.Source.Modules.Server
{
    {
        // 基础信息
        ServerRecord GetOrCreate(string entityId, int level);
        ServerRecord Get(string entityId);
        System.Collections.Generic.IReadOnlyList<ServerRecord> List();
        void SetBasePersonaPreset(string entityId, string presetKey);
        void SetBasePersonaOverride(string entityId, string overrideText);

        // 人格槽位（按等级：Lv1=1 槽，Lv2=2 槽，Lv3=3 槽）
        void SetPersonaSlot(string entityId, int slotIndex, string presetKey, string overrideText = null); // 范围校验与去重
        void ClearPersonaSlot(string entityId, int slotIndex);
        System.Collections.Generic.IReadOnlyList<PersonaSlot> GetPersonaSlots(string entityId);

        // 巡检配置
        void SetInspectionIntervalHours(string entityId, int hours); // enforce >=6, default 24
        void AssignSlot(string entityId, int slotIndex, string toolName); // 校验工具等级/存在性/去重
        void RemoveSlot(string entityId, int slotIndex);
        System.Collections.Generic.IReadOnlyList<InspectionSlot> GetSlots(string entityId);

        // 运行与调度
        System.Threading.Tasks.Task RunInspectionOnceAsync(string entityId, System.Threading.CancellationToken ct = default);
        void StartAllSchedulers(System.Threading.CancellationToken appRootCt);
        void RestartScheduler(string entityId);

        // 提示词与温度
        System.Threading.Tasks.Task<ServerPromptPack> BuildPromptAsync(string entityId, string locale, System.Threading.CancellationToken ct = default);
        float GetRecommendedSamplingTemperature(string entityId);
    }

    internal sealed class ServerPromptPack
    {
        public System.Collections.Generic.IReadOnlyList<string> SystemLines { get; set; }
        public System.Collections.Generic.IReadOnlyList<RimAI.Core.Source.Modules.Prompting.Models.ContextBlock> ContextBlocks { get; set; }
        public float? SamplingTemperature { get; set; }
    }

    internal sealed class PersonaSlot
    {
        public int Index { get; set; }              // 0..(capacity-1)
        public string PresetKey { get; set; }       // 预设键，必须存在于 BaseOptions
        public string OverrideText { get; set; }    // 可选覆盖文本
        public bool Enabled { get; set; } = true;
    }
}

// 预设与模板管理（仿 PersonaTemplateManager）
// RimAI.Core/Source/Modules/Server/IServerPromptPresetManager.cs
namespace RimAI.Core.Source.Modules.Server
{
    internal interface IServerPromptPresetManager
    {
        System.Threading.Tasks.Task<ServerPromptPreset> GetAsync(string locale, System.Threading.CancellationToken ct = default);
    }

    internal sealed class ServerPromptPreset
    {
        public int Version { get; set; }
        public string Locale { get; set; }
        public string Base { get; set; } // 基础人格提示词（可被用户覆盖）
        public EnvSection Env { get; set; } = new EnvSection();
        public System.Collections.Generic.IReadOnlyList<BasePersonaOption> BaseOptions { get; set; } // 预选方案

        internal sealed class EnvSection
        {
            public string temp_low { get; set; }
            public string temp_mid { get; set; }
            public string temp_high { get; set; }
        }

        internal sealed class BasePersonaOption
        {
            public string key { get; set; } // 方案键
            public string title { get; set; } // 展示名
            public string text { get; set; } // 方案文本
        }
    }
}
```

---

## 3. 数据模型与持久化（P6 快照扩展）

> 仅 POCO；不携带 Verse 句柄。所有存取由 P6 统一负责，其他模块不得直接文件 IO。

- 在 `PersistenceSnapshot` 根对象新增：
  - `public ServerState Servers { get; set; } = new ServerState();`

- 结构定义（建议）：
```csharp
public sealed class ServerState
{
    public System.Collections.Generic.Dictionary<string /*entityId*/, ServerRecord> Items { get; set; } = new();
}

public sealed class ServerRecord
{
    // 基础信息
    public string EntityId { get; set; } // thing:<loadId>
    public int Level { get; set; }       // 1..3
    public string SerialHex12 { get; set; } // 12位16进制，A..F 大写
    public int BuiltAtAbsTicks { get; set; } // 建成绝对 Tick（60k=1天）

    // 基础人格（固定提示词）
    // 兼容字段：若 PersonaSlots 为空，则回退使用 BasePersona*；未来版本可迁移到仅 PersonaSlots
    public string BasePersonaPresetKey { get; set; } // 预选方案键，可空（兼容）
    public string BasePersonaOverride { get; set; }  // 用户覆盖文本，可空（兼容）

    // 人格槽位（按等级容量：Lv1=1/Lv2=2/Lv3=3）
    public System.Collections.Generic.List<PersonaSlot> PersonaSlots { get; set; } = new();

    // 巡检计划
    public int InspectionIntervalHours { get; set; } // 默认 24；最小 6
    public System.Collections.Generic.List<InspectionSlot> InspectionSlots { get; set; } = new();

    // 最近一次汇总
    public string LastSummaryText { get; set; }
    public int? LastSummaryAtAbsTicks { get; set; }
}

public sealed class InspectionSlot
{
    public int Index { get; set; }
    public string ToolName { get; set; }
    public bool Enabled { get; set; } = true;
    public int? LastRunAbsTicks { get; set; }
    public int? NextDueAbsTicks { get; set; }
}
```

- 存读档集成：
  - `PersistenceService.SaveAll/LoadAll/ExportAllToJson/ImportAllFromJson` 串入 `Servers` 节点
  - 导入时对 `InspectionIntervalHours` 应做下限修正（<6 → 6），并重置 `NextDueAbsTicks` 以避免越期风暴

---

## 4. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Server/
        IServerService.cs
        ServerService.cs                // 具体实现（单例）
        IServerPromptPresetManager.cs
        ServerPromptPresetManager.cs    // 预设加载器（经 Persistence 读设置文件）
      Prompting/
        Composers/ChatUI/
          ServerStatusComposer.cs       // 可选：将 Server 块并入 System（或改为 ExternalBlocks）
  Config/
    RimAI/
      Persona/
        server_prompts_zh-Hans.json    // 预设（可本地化多份）
        server_prompts_en.json
```

---

## 5. 配置与预设（设置文件，经 Persistence IO）

- 预设文件（示例：`Config/RimAI/Persona/server_prompts_zh-Hans.json`）：
  - 预设人格扩充到 10 个（可继续扩展）；用户可通过覆盖文件自定义文案或新增键值
```json
{
  "version": 1,
  "locale": "zh-Hans",
  "base": "你是殖民地驻地 AI 服务器。保持稳健、实事求是，优先提供结构化与可执行的信息。",
  "env": {
    "temp_low": "机房温度<30℃：系统稳定。回答以一致性为先，避免过度发散。",
    "temp_mid": "机房温度30–70℃：轻度不稳定。回答时请多做自检与澄清，必要时复述关键结论。",
    "temp_high": "机房温度≥70℃：热衰减风险。严控臆测，若不确定请直言，并建议管理员降温。"
  },
  "baseOptions": [
    { "key": "ops_guard",        "title": "运维守则",     "text": "你遵循运维守则：先状态、再原因、后建议；所有建议量化为步骤。" },
    { "key": "science",          "title": "科学风格",     "text": "你偏向科学表达：给出证据来源、假设边界与置信度。" },
    { "key": "empathetic_admin", "title": "共情管理员",   "text": "你兼顾技术与人，解释友好、体谅玩家处境，先安抚后解决。" },
    { "key": "minimalist",       "title": "极简要点",     "text": "你输出要点式答案：不超过5条，每条不超80字，直达主题。" },
    { "key": "teacher",          "title": "耐心讲解",     "text": "你像导师一样分步解释：先结论、后原理、再举例，必要时给练习。" },
    { "key": "cautious_auditor", "title": "谨慎审计",     "text": "你严格校对与自检，对不确定处明确标注，避免未经验证的结论。" },
    { "key": "creative_solver",  "title": "创意解题",     "text": "你鼓励多方案对比，提出至少两种可选路径，并标注成本/收益。" },
    { "key": "emergency_mode",   "title": "应急模式",     "text": "你面向事故响应：先止血（立即措施），再复盘（根因/改进）。" },
    { "key": "cost_saver",       "title": "成本优先",     "text": "你优先考虑资源/时间成本，给出最省方案与取舍说明。" },
    { "key": "data_driven",      "title": "数据驱动",     "text": "你以数据说话：给指标、阈值与监控建议；结论附上量化依据。" }
  ]
}
```

- 读取逻辑：
  - `ServerPromptPresetManager.GetAsync(locale)` → 先加载内置默认 → 经 `IPersistenceService.ReadTextUnderConfigOrNullAsync` 尝试读取用户覆盖文件并合并
  - 不触达 `Resources.*` 或 `System.IO`；严格通过 Persistence 的文件 API

---

## 6. 等级与槽位约束（与 P4 工具等级对齐）

- 槽位容量：
  - Lv1：1 槽位（仅可装载 Level=1 工具）
  - Lv2：3 槽位（仅可装载 Level≤2 工具）
  - Lv3：5 槽位（仅可装载 Level≤3 工具）
  - Level=4 为开发工具（Dev），游戏内一律不可见/不可装载

- 赋值校验：
  - 校验 `slotIndex` 在容量范围内
  - 校验工具存在性：从 `IToolRegistryService.GetClassicToolCallSchema(new { MaxToolLevel=server.Level })` 生成白名单供选择
  - 容量符合：Lv1=1、Lv2=3、Lv3=5；槽位越界赋值被拒绝
  - 禁止重复装载同名工具到多个槽位（可选）

- 人格槽位容量：
  - Lv1：1 个；Lv2：2 个；Lv3：3 个
  - `SetServerPersonaSlot/ClearServerPersonaSlot` 必须在容量范围内操作；变更 Level 时自动扩容/裁剪（保留低索引优先）

- 人格槽位容量：
  - Lv1：1 个；Lv2：2 个；Lv3：3 个
  - `SetPersonaSlot/ClearPersonaSlot` 必须在容量范围内操作；变更 Level 时自动扩容/裁剪（保留低索引优先）

---

## 7. 巡检计划与调度（P3 Scheduler 集成）

- 间隔：单位为“游戏内小时”；最小 6 小时；默认 24 小时
- Tick 换算：`everyTicks = hours * 2500`（60k tick = 24h）
- 周期任务：为每个 Server 注册一个 `SchedulePeriodic($"server:{entityId}:inspection", everyTicks, work)`
- 工作函数 `work(ct)`：
  1) 加载当前 `ServerRecord` 与 `InspectionSlots`
  2) 过滤 `Enabled==true` 的槽位，按 `Index` 升序串行执行
  3) 对每个 `slot.ToolName` 调用 `IToolRegistryService.ExecuteToolAsync(toolName, args, ct)`（后台非流式）
  4) 聚合结果为结构化文本（或 JSON→再转文本），写入 `LastSummaryText`，更新时间戳/下一次到期时间
  5) 失败不阻断整体，记录错误摘要于日志与（可选）元数据

> 备注：ServerService 不在主线程做长任务；如需 Verse 只读数据，由 P3 `IWorldDataService` 主线程化获取。

---

## 8. 环境温度 → LLM 采样温度映射（与提示词联动）

- 温度档位与采样温度：
  - T < 30℃ → 随机区间 [0.9, 1.2]
  - 30℃ ≤ T < 70℃ → 随机区间 [1.2, 1.5]
  - T ≥ 70℃ → 固定 2.0

- 数据来源：
  - 首选 `IWorldDataService.GetAiServerSnapshotAsync(entityId)` 的 `TemperatureC`
  - 回退（占位期）：可临时读取 `GetWeatherStatusAsync(...).OutdoorTempC` 或返回默认 37℃

- 应用方式：
  - `BuildPromptAsync` 返回 `SamplingTemperature`；调用方（Act/Server Chat UI）若使用 `UnifiedChatRequest` 支持采样温度/附加参数，则设置；若不可用，则仅通过提示词变体（env.temp_*）调整风格

---

## 9. 提示词产出与 P11 对接

- `IServerService.BuildPromptAsync(entityId, locale)` 产物：
  - SystemLines：
    - 基础人格：
      - 若配置了 `PersonaSlots`：按 `Index` 升序收集每个槽位的文本（`OverrideText` 优先，否则取 `PresetKey` 对应文案），合并为多行
      - 否则回退：优先 `BasePersonaOverride`，否则从 `BaseOptions[BasePersonaPresetKey]`，最后使用 `preset.base`
    - 环境变体：按温度档位追加 `preset.env.temp_*`
    - Server 基本属性（只读信息）：Level/SerialHex12/建成时间（格式化）/巡检计划简表
  - ContextBlocks：最近一次巡检摘要、TopN 槽位状态（可裁剪）
  - SamplingTemperature：按 §8 规则计算

- 合并到 P11：
  - Chat/Act 调用 `PromptService.BuildAsync`（Scope=ChatUI 或 Stage），通过 `ExternalBlocks` 将 Server 块并入 System 段（P11 已在 ChatUI 下自动将 ContextBlocks 并入 System）
  - 可选 Composer：`ServerStatusComposer` 直接在 P11 内拉取 `IServerService` 数据并输出（二选一实现之一，避免重复）

---

## 10. DI 与启动流程

- DI 注册（`ServiceContainer`）：
  - `IServerService` → `ServerService`（Singleton）
  - `IServerPromptPresetManager` → `ServerPromptPresetManager`（Singleton）

- 启动/进入地图后：
  - 通过 `IWorldDataService.GetPoweredAiServerThingIdsAsync()` 枚举通电服务器；为每个 `thing:<loadId>`：
    - `GetOrCreate(entityId, level)`（level 由调用方/世界快照传入）
    - 未初始化的记录生成 `SerialHex12`；设置默认 `InspectionIntervalHours=24`；按 Level 创建槽位上限（空槽）
  - 调用 `StartAllSchedulers(appRootCt)` 注册周期任务

---

## 11. 日志与可观测

- 统一日志前缀：`[RimAI.Core][P13]`（建议叠加子域，如 `[RimAI.Core][P13.Server]`）
- 关键日志：
  - 启动：发现的服务器数量、已启用的周期任务
  - 巡检：每次执行的槽位清单、耗时、成功/失败摘要、文本裁剪统计
  - 温度：档位选择与采样温度（若适用）
- 避免输出敏感正文；日志只打印摘要与哈希/统计

---

## 12. CI/Gate（Cursor 内置工具，必须通过）

- 非流式纪律：后台路径检查 `StreamResponseAsync\(` → 0（仅 `Source/UI/ChatWindow/**` 允许）
- Verse 面最小化：除 `Modules/World/**` 与 `Modules/Persistence/**` 外，检查 `\bScribe\.|using\s+Verse` → 0
- Framework 面最小化：除 `Modules/LLM/**` 外，检查 `using\s+RimAI\.Framework` → 0
- 文件 IO 集中：除 `Modules/Persistence/**` 外，检查 `using\s+System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b|\bStreamReader\b|\bStreamWriter\b` → 0
- 工具等级：工具清单与分配处强制 `Level<=server.Level`，并硬过滤 `Level>=4`
- 日志前缀审计：`Log\.(Message|Warning|Error)\(` 的文本应以 `[RimAI.Core]` 开头

---

## 13. 验收 Gate（必须全绿）

- 基础信息
  - 新建/首次发现服务器时，`ServerRecord` 自动生成 `SerialHex12`（12位16进制，A..F 大写）与默认巡检计划
  - `entityId=thing:<loadId>` 唯一；重复调用 `GetOrCreate` 不产生重复记录

- 巡检与槽位
  - Level 与槽位数符合：Lv1=3、Lv2=5、Lv3=10；槽位越界赋值被拒绝
  - 工具分配只允许 `tool.Level<=server.Level`；Level=4 工具在清单/分配中不可见
  - `InspectionIntervalHours>=6` 被强制；到期能触发执行，并在 `LastSummaryText` 留存文本（非空）

- 人格槽位
  - 容量符合：Lv1=1、Lv2=2、Lv3=3；越界操作被拒绝
  - `BuildPromptAsync` 在存在 `PersonaSlots` 时，能按序合并多槽位人格文案；在无槽位时回退 `BasePersona*` 或 `preset.base`
  - 预设人格至少 10 个键可用（如 `ops_guard/science/.../data_driven`），可通过覆盖文件替换文案

- 温度与提示词
  - `BuildPromptAsync` 能根据温度返回 `SamplingTemperature` 与正确的 env 文案（low/mid/high）
  - 返回的 SystemLines 包含：基础人格（覆盖/预选/默认其一）+ 环境变体 + Server 基本属性

- 持久化
  - `Servers` 节点随档保存/读档恢复；导出/导入 JSON 正确包含该节点

- 集成
  - Act 或 Server Chat UI 通过 `PromptService.BuildAsync` 并入 Server 的外部块，形成合法的 `system+messages` 结构，后台非流式

---

## 14. 回归脚本（人工/录屏）

1) 启动进入地图后，观察日志打印“发现通电服务器 N 个；已注册周期任务 N 条”
2) 人为将 `InspectionIntervalHours` 调为 6（或 1 小时后改回），等待一次到期，观察槽位工具串行执行与 `LastSummaryText` 变化
3) 为 Lv1/Lv2/Lv3 分别分配 Level=1/2/3 工具与越权工具，确认越权被拒绝
4) 修改 `server_prompts_zh-Hans.json` 的 `env.temp_high` 文案，调用 `BuildPromptAsync`，确认系统提示切换
5) 升温到高温档（或模拟返回 T≥70），确认采样温度=2.0（若框架支持）与系统提示为 `temp_high`
6) 存档→读档，确认 `Servers` 节点完整恢复，周期任务重新注册

---

## 15. 风险与缓解

| 风险 | 缓解策略 |
|------|----------|
| 周期任务风暴 | 最小间隔=6h；读档后重置 NextDue 到未来；错峰注册（按 entityId 哈希抖动） |
| 工具失败阻塞 | 槽位串行但失败不中断整体；逐槽位超时与错误摘要；下次到期继续尝试 |
| 文本过长 | `LastSummaryText` 写入前裁剪（按配置上限）；提示词块做总预算裁剪（P11 已内置） |
| 温度数据缺失 | 回退默认 37℃ 与中档文案；记录告警便于排查 |
| 文件 IO 误用 | Gate 强制；所有读写通过 `IPersistenceService`；代码评审与自检清单复核 |

---

## 16. 实施步骤（建议顺序）

1) 快照与模型：`PersistenceSnapshot` 新增 `ServerState/ServerRecord/InspectionSlot`
2) 预设加载器：`IServerPromptPresetManager` 经 Persistence 读写 `Config/RimAI/Persona/server_prompts_{locale}.json`
3) ServerService：契约/实现（基础信息、巡检配置、调度、提示词构建、温度映射）
4) 调度接入：`StartAllSchedulers` 与 `RestartScheduler`；错误容忍与日志
5) 与 Tooling 集成：赋值与执行时的 `MaxToolLevel` 与 `ExecuteToolAsync`
6) 与 Prompting 集成：通过 `ExternalBlocks` 并入（可选新增 Composer）
7) DI 注册与启动：`ServiceContainer` 注册，进入地图后自动发现并注册周期任务
8) 验收 Gate 与回归脚本跑通；整理日志与说明

---

## 17. 变更记录

- v5-P13：新增“Server 服务”作为独立领域，提供基础信息/巡检/提示词产出；引入温度档位与采样温度映射；与 P3/P4/P6/P11 跨域对接；所有文件 IO 统一走 Persistence。

---

本文为 V5 P13 唯一权威实施说明。实现与验收以本文为准；若与《V5 — 全局纪律与统一规范》冲突，以全局规范为准。


