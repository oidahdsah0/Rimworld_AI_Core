# RimAI V5 — P11 实施计划（提示词服务：Prompting）

> 目标：交付一个“提示词服务（Prompting Service）”，以可插拔作曲器（Composer）方式组织系统提示词与上下文内容块，供 ChatUI/Stage/Tool 等场景通过单一入口调用。满足多语言（JSON 资源）与热插拔（配置热重载）要求；并新增世界数据快照以覆盖社交关系与社交历史。本文件为唯一入口，无需翻阅其他文件即可完成落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（`docs/V5_GLOBAL_CONVENTIONS.md`）与《V5 架构文档》（`docs/ARCHITECTURE_V5.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 新增 `Source/Modules/Prompting/**`：统一的提示词服务，单一入口 `IPromptService.BuildAsync(...)`。
  - 引入“可插拔作曲器（Composer）”机制：按场景（Scope）与顺序（Order）组合不同信息块；支持启停与热重载。
  - 多语言资源：本地化 JSON（zh-Hans / en 起步），统一 `ILocalizationService` 读取，缺键自动回退。
  - P3 世界数据扩展：一次性提示词用快照（身份/特质/技能/信仰可用性）+ 社交快照（社交关系/社交历史）。
  - ChatUI 专用作曲器集合：满足用户提出的 12 项信息需求（含新增“社交关系/社交历史”）。

- 非目标（后续阶段/其他模块处理）
  - 不改变 LLM/Tooling/Orchestration 的内部契约；本阶段仅组织提示词与上下文。
  - 不在本阶段实现 UI 绘制或布局改动（由 P10 已交付的 ChatUI 负责）。
  - 不扩展 `RimAI.Core.Contracts` 的对外合同（保持 V5 最小面）。

---

## 1. 架构总览（全局视角）

- 依赖关系（最小耦合）
  - P1 `IConfigurationService`：读取 Prompting/Localization 配置；热重载广播。
  - P3 `IWorldDataService`：只读世界数据快照（主线程化访问 Verse）。
  - P6 `IPersistenceService`：读取本地化 JSON 资源（只读路径，统一 IO）。
  - P7 `IPersonaService`/`IPersonaJobService`：人格信息块（职务/传记/意识形态/固定提示）。
  - P8 `IHistoryService`/`IRecapService`/`IRelationsService`：历史摘要与关联对话。
  - P2 `ILLMService`：仅由上层消费；本阶段不直接依赖（保持边界）。

- 不变式（与 V5 对齐）
  - 访问边界：Verse 仅 P3；Framework 仍仅 P2；文件 IO 统一 P6。
  - 非流式纪律：Prompting/Composer 全链非流式；仅 UI/Debug 允许流式展示，不在本阶段实现。
  - 历史域：仅做摘要与相关会话查询；不落中间 chunk 文本到系统提示中（历史对话作为内容传输）。
  - 日志：统一前缀 `[RimAI.Core][P11]`（叠加 `[RimAI.Core]` 全局要求）。

---

## 2. 目录结构与文件

```
RimAI.Core/
  Source/
    Modules/
      Prompting/
        IPromptService.cs               // 单入口接口：BuildAsync
        PromptService.cs                // 聚合作曲器、裁剪、日志、热重载
        IPromptComposer.cs              // 作曲器接口（Scope/Order/Id/ComposeAsync）
        PromptComposerAttribute.cs      // 作曲器声明元数据（可选）
        Models/
          PromptModels.cs              // PromptBuildRequest/Result/ContextBlock/PromptScope/ComposerOutput
        Composers/
          ChatUI/
            PawnIdentityComposer.cs
            PawnBackstoryComposer.cs
            PawnTraitsComposer.cs
            PawnSkillsComposer.cs
            PersonaJobComposer.cs
            PersonaBiographyComposer.cs
            PersonaIdeologyComposer.cs
            PersonaFixedPromptComposer.cs
            PawnSocialRelationsComposer.cs
            HistoryRecapComposer.cs
            RelatedConversationsComposer.cs
            PawnSocialHistoryComposer.cs
    Localization/
      Locales/
        zh-Hans.json                    // 多语言资源（示例键见 §6）
        en.json
  docs/
    V5_P11_IMPLEMENTATION_PLAN.md      // 本文件
```

---

## 3. 接口契约与模型（服务与作曲器）

```csharp
// PromptModels.cs（要点）
internal enum PromptScope { ChatUI, Stage, Tool }

internal sealed class PromptBuildRequest {
  public PromptScope Scope { get; init; }
  public string ConvKey { get; init; }
  public System.Collections.Generic.IReadOnlyList<string> ParticipantIds { get; init; }
  public int? PawnLoadId { get; init; }
  public bool IsCommand { get; init; }
  public string Locale { get; init; }
  public string UserInput { get; init; }
}

internal sealed class ContextBlock {
  public string Title { get; init; }
  public string Text { get; init; }
}

internal sealed class PromptBuildResult {
  public string SystemPrompt { get; init; }              // 固定基底 + 动态段（多语言）
  public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; init; } // 历史/关联/社交历史
  public string UserPrefixedInput { get; init; }         // 多语言前缀 + 原始输入
}

internal sealed class PromptBuildContext { // 作曲器运行期上下文（便于复用）
  public PromptBuildRequest Request { get; init; }
  public string Locale { get; init; }
  public string EntityId { get; init; } // 如 pawn:<loadId>
  // 预先取好的只读快照（由 PromptService 聚合）
  public RimAI.Core.Source.Modules.World.PawnPromptSnapshot PawnPrompt { get; init; }
  public RimAI.Core.Source.Modules.World.PawnSocialSnapshot PawnSocial { get; init; }
  public RimAI.Core.Source.Modules.Persona.PersonaRecordSnapshot Persona { get; init; }
  public System.Collections.Generic.IReadOnlyList<RimAI.Core.Source.Modules.History.Recap.RecapItem> Recaps { get; init; }
}

internal sealed class ComposerOutput {
  public System.Collections.Generic.IReadOnlyList<string> SystemLines { get; init; }
  public System.Collections.Generic.IReadOnlyList<ContextBlock> ContextBlocks { get; init; }
}

internal interface IPromptService {
  System.Threading.Tasks.Task<PromptBuildResult> BuildAsync(PromptBuildRequest request, System.Threading.CancellationToken ct = default);
}

internal interface IPromptComposer {
  PromptScope Scope { get; }
  int Order { get; }
  string Id { get; }
  System.Threading.Tasks.Task<ComposerOutput> ComposeAsync(PromptBuildContext ctx, System.Threading.CancellationToken ct);
}
```

设计说明：
- 单一入口 `IPromptService.BuildAsync`：ChatUI/Stage/Tool 统一调用。
- `PromptService` 负责：装配作曲器流水线 → 执行 → 合并 SystemLines/ContextBlocks → 多语言前缀用户输入 → 裁剪预算。
- 作曲器按照 `Scope/Order` 过滤与排序；可用配置启停；热重载即时生效。

---

## 4. ChatUI 作曲器清单（覆盖 12 项 + 新增社交）

- 进入 SystemPrompt 动态段（无历史 chunk）：
  1) `PawnIdentityComposer`：名称/性别/年龄/人种/信仰（DLC 可用时）
  2) `PawnBackstoryComposer`：童年/成年身份
  3) `PawnTraitsComposer`：特性、无法从事
  4) `PawnSkillsComposer`：各项技能（等级/热情或归一显示）
  5) `PersonaJobComposer`：人格-职务（若有）
  6) `PersonaBiographyComposer`：人格-个人传记（若有）
  7) `PersonaIdeologyComposer`：人格-意识形态（若有）
  8) `PersonaFixedPromptComposer`：人格-固定提示词（若有）
  9) `PawnSocialRelationsComposer`：社交关系（配偶/恋人/朋友/仇敌/TopN 好感）

- 作为 ContextBlocks（内容传输）：
  10) `HistoryRecapComposer`：前情提要（P8 Recap 最新 N 段）
  11) `RelatedConversationsComposer`：关联对话（P8 Relations → 最近 M 条 AI Final）
  12) `PawnSocialHistoryComposer`：社交历史（最近 K 条社交互动事件）

说明：历史/关联/社交历史不进入 SystemPrompt，仅作为内容块传输。

---

## 5. 本地化（多语言 JSON）

- 统一接口 `ILocalizationService`（内部）：
  - `string Get(string locale, string key, string fallback = "")`
  - `string Format(string locale, string key, object args)`
  - `event Action<string> OnLocaleChanged`
  - 资源路径：`Localization/Locales/{locale}.json`（构建复制到 Mod 包）；实际读取通过 `IPersistenceService` 提供的只读资源 IO。

- 关键键（建议）：
  - ChatUI 基底与前缀：
    - `ui.chat.system.base`
    - `ui.chat.user_prefix`
  - 段落标题：
    - `prompt.section.identity`
    - `prompt.section.backstory`
    - `prompt.section.traits`
    - `prompt.section.work_disables`
    - `prompt.section.skills`
    - `prompt.section.job`
    - `prompt.section.biography`
    - `prompt.section.ideology`
    - `prompt.section.fixed_prompts`
    - `prompt.section.social_relations`
    - `prompt.section.social_history`
    - `prompt.section.recap`
    - `prompt.section.related_conversations`
  - 行格式模板：
    - `prompt.format.skill`（如：`"{name}: Lv{level}{passion}"`）
    - `prompt.format.relation`（如：`"{rel}: {name} ({opinion})"`）
    - `prompt.format.social_event`（如：`"[{time}] {who}: {kind}{outcome}"`）
    - `prompt.format.backstory`
    - `prompt.format.traits_line`
    - `prompt.format.work_disables_line`

- 回退策略：缺键 → 使用内置默认中文/英文常量；记录告警但不中断。

---

## 6. P3 世界数据扩展（新快照）

```csharp
// IWorldDataService（新增）
Task<PawnPromptSnapshot> GetPawnPromptSnapshotAsync(int pawnLoadId, System.Threading.CancellationToken ct = default);
Task<PawnSocialSnapshot> GetPawnSocialSnapshotAsync(int pawnLoadId, int topRelations, int recentSocialEvents, System.Threading.CancellationToken ct = default);

// Models.cs（新增/扩展）
internal sealed class PawnPromptSnapshot {
  public Identity Id { get; set; }                 // 名称/性别/年龄/人种
  public Backstory Story { get; set; }             // 童年/成年
  public TraitsAndWork Traits { get; set; }        // 特性、无法从事
  public Skills Skills { get; set; }               // 技能数组（名称/等级/热情或归一）
  public bool IsIdeologyAvailable { get; set; }    // DLC/数据可用探测
}

internal sealed class PawnSocialSnapshot {
  public System.Collections.Generic.IReadOnlyList<SocialRelationItem> Relations { get; set; }
  public System.Collections.Generic.IReadOnlyList<SocialEventItem> RecentEvents { get; set; }
}

internal sealed class SocialRelationItem {
  public string RelationKind { get; set; }         // Spouse/Fiance/Lover/Friend/Rival/Enemy
  public string OtherName { get; set; }
  public string OtherEntityId { get; set; }        // pawn:<loadId>
  public int Opinion { get; set; }                 // -100..100
}

internal sealed class SocialEventItem {
  public System.DateTime TimestampUtc { get; set; }
  public string WithName { get; set; }
  public string WithEntityId { get; set; }
  public string InteractionKind { get; set; }      // Chitchat/Insult/Kind words/...
  public string Outcome { get; set; }              // 好/中/差（可选）
}
```

实现要点：
- 全部通过 `ISchedulerService.ScheduleOnMainThreadAsync` 在主线程读取 Verse。
- Ideology DLC：`ModsConfig.IdeologyActive` 探测，未启用则跳过信仰相关字段。
- 社交关系：`pawn.relations.DirectRelations` + `OpinionOf(other)`；TopN 依据 Opinion/重要程度。
- 社交历史：从 `PlayLog/InteractionsLog` 筛选与该 pawn 相关的最近 K 条互动记录。
- 返回纯 POCO，不携带 Verse 类型或句柄。

---

## 7. 组织策略与裁剪

- SystemPrompt = `ui.chat.system.base` + 动态段（由各作曲器输出的 SystemLines 依序拼接）。
- ContextBlocks = 历史/关联/社交历史，不进入 SystemPrompt。
- 用户输入前缀：`ui.chat.user_prefix` + 空格 + 原始输入 → 返回 `UserPrefixedInput`。
- 裁剪优先级：ContextBlocks → 动态段末尾 → 保留基底完整；预算来自配置（§8）。

---

## 8. 配置（内部建议，保持对外最小面）

```json
{
  "UI": {
    "ChatWindow": {
      "Prompts": {
        "Locale": "zh-Hans",
        "MaxSystemPromptChars": 1600,
        "MaxRecapBlocks": 2,
        "MaxRelatedConvs": 2,
        "MaxAiLinesPerRelated": 3,
        "Social": { "TopRelations": 5, "RecentEvents": 5 },
        "Composers": {
          "ChatUI": {
            "Enabled": [
              "pawn_identity", "pawn_backstory", "pawn_traits", "pawn_skills",
              "persona_job", "persona_biography", "persona_ideology", "persona_fixed",
              "pawn_social_relations", "history_recap", "related_conversations", "pawn_social_history"
            ],
            "Order": [
              "pawn_identity", "pawn_backstory", "pawn_traits", "pawn_skills",
              "persona_job", "persona_biography", "persona_ideology", "persona_fixed",
              "pawn_social_relations", "history_recap", "related_conversations", "pawn_social_history"
            ]
          }
        }
      }
    }
  }
}
```

说明：
- 热重载：`IConfigurationService.OnConfigurationChanged` 触发 PromptService 重建流水线。
- Locale 可跟随游戏语言或由配置覆盖；`ILocalizationService` 广播语言切换事件。

---

## 9. 线程与纪律（必须遵守）

- 非流式纪律：Prompting/Composer 全链非流式；后台禁用 `StreamResponseAsync(`。
- 访问边界：Verse 仅 P3 世界服务；Framework 仅 P2；文件 IO 仅 P6；其余模块禁用直触。
- 主线程守则：任何 Verse 访问均经 `ISchedulerService` 主线程化，调用方以 `Task` 等待，禁止 `.Wait()`/`.Result`。
- 日志：统一 `[RimAI.Core][P11]`，不落敏感正文；记录键/长度/预算等指标。

---

## 10. 实施步骤（一步到位）

- S1：建立目录与骨架（`Modules/Prompting/**`），定义 `IPromptService`、`IPromptComposer`、模型与属性类。
- S2：实现 `PromptService`：
  - 发现与装配作曲器（构造注入 + 可选反射 Attribute）；
  - 读取配置/本地化；监听热重载事件；
  - 聚合所需快照（P3/P7/P8），构造 `PromptBuildContext`；
  - 执行作曲器流水线并合并结果；多语言前缀；裁剪与日志。
- S3：多语言：实现 `ILocalizationService` + `Locales/zh-Hans.json`、`Locales/en.json`（含必要键）。
- S4：P3 扩展：`GetPawnPromptSnapshotAsync`、`GetPawnSocialSnapshotAsync`（主线程化 Verse 访问）。
- S5：实现 ChatUI 作曲器集合（§4 所列 12 个）。
- S6：将 `ChatController` 改为仅调用 `IPromptService.BuildAsync(...)` 单入口；闲聊/命令共用。
- S7：配置热重载/语言切换验证；DLC 有/无、数据缺失容错自测。
- S8：Gate 自检 + 回归录屏。

---

## 11. 验收 Gate（必须全绿）

- 功能与数据
  - ChatUI 通过单入口获取 SystemPrompt/ContextBlocks/UserPrefixedInput。
  - 12 项信息需求全部覆盖；DLC 不可用或数据缺失时自动跳过相应段落。
  - 社交关系/社交历史数据正确；TopN/最近K条受配置控制。
  - 历史与关联对话仅作为 ContextBlocks，不进入 SystemPrompt。

- 多语言
  - 切换 `Locale` 后，基底、段落标题与行模板即时切换；缺键有回退。

- 热插拔
  - 通过配置启停某个作曲器，结果即时变化；无需重启游戏。

- 纪律与边界
  - 后台路径 `StreamResponseAsync\(` 仓级计数=0（允许路径仅 UI/Debug）。
  - 访问边界检查：除 `Modules/World/**`、`Modules/Persistence/**` 外不出现 `using Verse|\bScribe\.`；除 `Modules/LLM/**` 外不出现 `using RimAI.Framework`；除 `Modules/Persistence/**` 外不出现 `System.IO` 直接使用。
  - 依赖注入仅“构造函数注入”；禁止属性注入与 Service Locator。

- 性能
  - 本地数据组装 ≤ 200ms；无明显 GC 峰值；裁剪按预算执行。

---

## 12. 回归脚本（人工/录屏）

1) 进入游戏，选择任意小人，在 ChatUI 中发送两次（闲聊/命令各一次）。
2) 观察：
   - SystemPrompt 包含身份/特质/技能/人格/社交关系等动态段；
   - ContextBlocks 含前情提要、关联对话、社交历史（若有数据）。
3) 切换语言为英文，再次发送：
   - 段落标题/模板/用户前缀切换为英文；缺失键不报错。
4) 在配置中禁用 `persona_ideology` 作曲器：
   - 再发送一次，不应再出现“意识形态”段落。
5) 关闭/启用 Ideology DLC：
   - 对比提示词中的信仰相关字段是否正确增减。

---

## 13. CI / Gate（使用 Cursor 内置工具，必须通过）

- 流式纪律（强制）：
  - 检查=0：后台/服务路径 `StreamResponseAsync\(`。
  - 允许匹配：`Source/UI/ChatWindow/**` 与 `Source/UI/DebugPanel/**`（本阶段代码不应新增流式）。

- 访问边界：
  - 检查=0：除 `Modules/LLM/**` 外 `using\s+RimAI\.Framework`。
  - 检查=0：除 `Modules/World/**`、`Modules/Persistence/**` 外 `using\s+Verse|\bScribe\.`。
  - 检查=0：除 `Modules/Persistence/**` 外 `using\s+System\.IO|\bFile\.|\bDirectory\.|\bFileStream\b`。

- 注入纪律：
  - 检查=0：属性注入与 Service Locator 约定模式。

- 日志前缀（建议性 Gate）：
  - 抽样 `Log\.(Message|Warning|Error)\(` 文本以 `[RimAI.Core]` 开头，建议叠加 `[P11]`。

---

## 14. 性能与可靠性预算

- 组装耗时：常态 ≤ 200ms（不含上游网络）。
- 内存与 GC：避免大字符串反复拼接；建议复用 `StringBuilder` 与对象池（作曲器内部）。
- 可靠性：配置/本地化缺失不致命；作曲器局部失败不影响全体（跳过并记录日志）。

---

## 15. 安全与隐私

- 日志不落敏感正文；记录键/会话哈希/长度与预算指标。
- 历史域仅保存最终输出与必要元信息；Prompting 不扩散中间 chunk。
- 本地化 JSON 仅含段落/模板，不包含用户与剧情具体内容。

---

## 16. 风险与缓解

| 风险 | 缓解 |
|------|------|
| 作曲器过多导致提示词超长 | 统一预算 + 分级裁剪（先裁 ContextBlocks，再裁动态段末尾） |
| DLC/数据缺失导致空引用 | 严格空值检测 + DLC 探测；缺失即跳过 |
| 多语言键缺失 | 本地化服务回退到内置常量；记录告警 |
| 热重载时态竞争 | PromptService 内部使用轻量锁与快照替换，避免半成品状态 |

---

## 17. FAQ（常见问题）

- Q：为什么历史对话不放入 SystemPrompt？
  - A：V5 规范要求系统提示词保持稳定与精炼；历史作为“内容传输”更可控，且便于裁剪与复用。

- Q：如何新增一个作曲器？
  - A：在 `Composers/<Scope>/` 下实现 `IPromptComposer`，声明 `Id/Order/Scope`，在配置 `Enabled/Order` 中启用即可，无需修改核心服务。

- Q：与 ChatUI 的关系？
  - A：ChatUI 仅调用 `IPromptService` 单入口；闲聊使用真流式展示、命令伪流式展示（见 P10），但两者的提示词组织均由本服务提供。

---

## 18. 变更记录

- v5-P11（初版）：
  - 新增 `Prompting` 服务与可插拔作曲器；单入口；多语言 JSON；配置热重载。
  - 扩展 P3：提示词快照 + 社交快照；覆盖社交关系/社交历史。
  - ChatUI 作曲器覆盖身份/特质/技能/人格/社交/历史/关联。
  - Gate：非流式纪律、访问边界、注入纪律与日志前缀对齐 V5。


