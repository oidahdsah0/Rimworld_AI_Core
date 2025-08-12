# RimAI V5 — P1 实施计划（合并 P0+P1：Skeleton + DI & Config）

> 目标：一次性交付“可启动、可自检、可配置、可热重载”的最小内核，为后续 P2+ 模块插拔提供稳定底座。本文档为唯一入口文档，无需回看旧文即可完成落地与验收。

> 全局对齐：本阶段遵循《V5 — 全局纪律与统一规范》（见 `docs/V5_GLOBAL_CONVENTIONS.md`）。若本文与全局规范冲突，以全局规范为准。

---

## 0. 范围与非目标

- 范围（本阶段交付）
  - 基础设施：`ServiceContainer`（DI 容器，单例、构造函数注入、预热、自检、环依赖检测）
  - 配置系统：`IConfigurationService`（Contracts 只读接口 + Core 实现）；`CoreConfig`（内部）→ `CoreConfigSnapshot`（对外只读）
  - 可观测性：最小 Debug 面板与日志（Ping、ResolveAll、Config 预览与热重载回显）
  - 文档与 Gate：接口签名冻结、目录规范、Gate 清单（Cursor 内置工具）、最小回归脚本

- 非目标（后续阶段处理）
  - LLM 网关与外部 API（P2）
  - 工具系统 / 事件聚合 / 舞台 / 历史（P3+）
  - 复杂主线程调度与世界访问（P3+）

---

## 1. 架构总览（全局视角）

- 分层边界
  - Contracts（稳定对外层）：对第三方/上层仅暴露只读配置接口与最小 DTO
  - Core 实现层（内部）：容器、配置加载/热重载、调试面板与诊断日志

- 生命周期与原则
  - 所有服务默认单例，严格“构造函数注入”，禁止属性注入
  - 启动期一次性预热构造所有已注册单例，失败快速失败（Fail Fast）
  - 配置采用“不可变快照”模型；热重载通过事件广播，新快照立即可见

- 可观测
  - 启动横幅、容器健康检查、配置快照预览、热重载提示

---

## 2. 目录结构与命名规范

建议按如下目录组织（文件路径为推荐；保持与实际项目命名一致即可）：

```
RimAI.Core/
  Source/
    Infrastructure/
      ServiceContainer.cs
      Configuration/
        ConfigurationService.cs
        CoreConfig.cs                // 内部实现模型（比快照更全）
    UI/DebugPanel/Parts/
      P1_PingButton.cs
      P1_ResolveAllButton.cs
      P1_ConfigPreview.cs
RimAI.Core.Contracts/
  Config/
    IConfigurationService.cs        // 只读接口（稳定）
    CoreConfigSnapshot.cs           // 对外只读快照（稳定）
```

---

## 3. 契约（Contracts 稳定层）

只暴露对外“最小可见面”，接口签名一经发布即冻结（新增仅加不减）。

```csharp
// RimAI.Core.Contracts/Config/IConfigurationService.cs
namespace RimAI.Core.Contracts.Config {
  public interface IConfigurationService {
    CoreConfigSnapshot Current { get; }
    event Action<CoreConfigSnapshot> OnConfigurationChanged;
  }
}

// RimAI.Core.Contracts/Config/CoreConfigSnapshot.cs
namespace RimAI.Core.Contracts.Config {
  public sealed class CoreConfigSnapshot {
    public string Version { get; init; }             // e.g. "v5-P1"
    public string Locale { get; init; }              // e.g. "zh-Hans"
    public bool DebugPanelEnabled { get; init; }
    public bool VerboseLogs { get; init; }
  }
}
```

说明：
- Snapshot 仅包含对外必要字段；内部可在 `CoreConfig` 中扩展更多节点，不影响对外兼容。

---

## 4. Core 实现层设计

### 4.1 ServiceContainer（DI 容器）

职责：统一注册与构造单例；严格构造函数注入；预热、自检、环依赖与失败快速失败；对 UI 提供健康信息。

关键要求：
- 注册 API：`Register<TInterface, TImplementation>()`、`RegisterInstance<TInterface>(instance)`、`Resolve<TInterface>()`、`ResolveAll()`
- 单例生命周期：每个接口仅有一个实例；不允许在构造后被替换
- 构造规则：反射找到“唯一最长参数”的公共构造函数，递归 Resolve 其依赖
- 环依赖检测：构造栈维护（`Stack<Type>`），发现回路立刻抛出详细异常（链路打印）
- 预热：`Init()` 完成后，立即执行 `ResolveAll()`，记录耗时/失败项
- 诊断：提供 `IReadOnlyList<ServiceHealth>`（名称、构造耗时、状态、错误消息）

伪代码（要点）：
```csharp
public sealed class ServiceContainer {
  private readonly Dictionary<Type, Func<object>> _registrations = new();
  private readonly Dictionary<Type, object> _singletons = new();
  private readonly Dictionary<Type, TimeSpan> _constructTimes = new();

  public void Register<TI, T>() where T: class, TI { /* store factory using reflection ctor */ }
  public void RegisterInstance<TI>(TI instance) { /* store singleton directly */ }

  public void Init() {
    // 预热构造：对全部注册接口执行 Resolve（捕获并记录错误 → Fail Fast）
    foreach (var type in _registrations.Keys) Resolve(type);
  }

  public object Resolve(Type t) {
    if (_singletons.TryGetValue(t, out var inst)) return inst;
    var sw = Stopwatch.StartNew();
    var stack = new Stack<Type>();
    var created = CreateWithCtorInjection(t, stack);
    sw.Stop();
    _singletons[t] = created; _constructTimes[t] = sw.Elapsed;
    return created;
  }
}
```

### 4.2 ConfigurationService（配置加载与热重载）

职责：从 RimWorld ModSettings 加载内部 `CoreConfig`，映射为只读 `CoreConfigSnapshot` 输出；支持 `Reload()` 生成新快照并广播事件。

设计要点：
- 不可变：对外返回的 `CoreConfigSnapshot` 必须是不可变对象（init-only）
- 默认值：当缺失设置时，提供稳定默认值（见 §8.1）
- 事件：`OnConfigurationChanged` 在快照替换后触发；订阅方自行捕获并读取新快照
- 本地化：`Locale` 作为全局默认语言（UI/模板后续使用）

接口（Core 内部实现示意）：
```csharp
public sealed class ConfigurationService : IConfigurationService {
  private CoreConfig _current; // 内部模型
  public CoreConfigSnapshot Current => MapToSnapshot(_current);
  public event Action<CoreConfigSnapshot> OnConfigurationChanged;

  public ConfigurationService(/* RimWorld settings adapter */) {
    _current = LoadFromModSettingsOrDefaults();
  }

  public void Reload() {
    _current = LoadFromModSettingsOrDefaults();
    OnConfigurationChanged?.Invoke(MapToSnapshot(_current));
  }
}
```

### 4.3 Debug 面板（P1 最小功能）

- Ping：打印 `pong`、容器服务总数与版本号（Version 来自快照）
- ResolveAll：遍历并显示所有单例状态（OK/Failed）与构造耗时；失败项展开错误摘要
- Config 预览：显示当前 `CoreConfigSnapshot` JSON；点击“Reload”调用 `IConfigurationService.Reload()` 并提示新时间戳

UI 要点：
- 按钮与输出均带统一前缀 `[RimAI.P1]`，便于日志过滤
- 当 `DebugPanelEnabled=false` 时隐藏入口（保留强制快捷键开关用于开发）

### 4.4 日志与诊断

- 启动横幅：`[RimAI.P1] Boot OK (services=N, elapsed=xxx ms)`
- 配置热重载：`[RimAI.P1] Config Reloaded (version=..., locale=..., at=...)`
- 失败快速失败：记录完整依赖链路与提示“请打开 Debug 面板查看 ResolveAll 详情”

---

## 5. 实施步骤（一步到位）

> 按顺序完成 S1→S4，期间可随时通过 Debug 面板与日志进行自检。所有文件与接口签名均在本节中给出，无需查阅其他文档。

### S1：实现与接线 ServiceContainer

1) 新建 `Source/Infrastructure/ServiceContainer.cs`
   - 提供注册、解析、预热、健康信息 API（见 §4.1）
   - 实现构造函数注入与环依赖检测；缓存构造函数以降开销
2) 在 Mod 启动入口调用
   - `ServiceContainer.Register<IConfigurationService, ConfigurationService>()`
   - 暂不注册其他 P2+ 服务（保持最小）
3) 调用 `ServiceContainer.Init()`
   - 预热构造所有注册单例；失败则抛出异常中止加载
4) 输出启动横幅与摘要

### S2：配置系统（内部 CoreConfig → 对外 Snapshot）

1) 新建 `Source/Infrastructure/Configuration/CoreConfig.cs`
   - 结构包含：`General/Diagnostics/UI` 节点；为后续留空位（见 §8.1）
2) 新建 `Source/Infrastructure/Configuration/ConfigurationService.cs`
   - 从 RimWorld `ModSettings` 读取；不可用时回落默认值
   - 首次构造 `CoreConfig` → 映射 `CoreConfigSnapshot`
   - `Reload()`：重新读取并广播新快照
3) 新建 Contracts
   - `RimAI.Core.Contracts/Config/IConfigurationService.cs`
   - `RimAI.Core.Contracts/Config/CoreConfigSnapshot.cs`
4) 在 `ServiceContainer` 中注册 `IConfigurationService`

### S3：Debug 面板（最小三件套）

1) 新建 3 个 UI 组件（可合并在一个类内分按钮）：
   - `P1_PingButton`：输出容器健康简报 + 当前快照版本
   - `P1_ResolveAllButton`：遍历输出每个服务的状态与构造耗时
   - `P1_ConfigPreview`：展示 `CoreConfigSnapshot` JSON；含“Reload”按钮
2) 仅当 `DebugPanelEnabled=true` 时在 UI 挂载入口添加这三项
3) 本地化字符串：`Keys.RimAI.P1.Ping/ResolveAll/ConfigPreview`

### S4：文档、脚本与 Gate（人工或 CI 皆可执行）

1) Gate（保证 P1 纪律，使用 Cursor 内置工具执行）
   - 禁止属性注入：搜索属性 setter 滥用
   - 容器唯一入口：搜索 `new ServiceContainer(` 仅应出现在启动处
2) 最小回归脚本（人工）
   - 进入游戏 → 打开 Debug 面板 → 依次点击三按钮 → 日志观察关键输出
3) 文档标注本文件为 P1 标准；接口签名冻结点：`IConfigurationService` / `CoreConfigSnapshot`

---

## 6. 验收 Gate（必须全绿）

- 引导与日志
- 启动不报错；日志出现：`[RimAI.Core][P1] Boot OK (services=N, elapsed=xxx ms)`
- Debug 面板
  - Ping：输出 `pong`、服务数与版本号
  - ResolveAll：所有注册项均 OK，构造耗时 < 100ms（阈值可调）；若失败显示详细异常与依赖链
  - Config 预览：显示 JSON；点击 Reload 后 1s 内出现热重载提示，Snapshot 版本/时间戳更新
- 接口签名冻结
  - `IConfigurationService` 与 `CoreConfigSnapshot` 与本文一致（字段名/语义不可破坏性修改）
- 性能预算
  - 容器预热 ≤ 200ms；每帧新增 0ms（P1 未挂载循环逻辑）

---

## 7. 快速上手（开发者 5 分钟路径）

1) 打开 `ServiceContainer.cs`，确认 `Register<IConfigurationService, ConfigurationService>()` 存在
2) 运行游戏，查看日志横幅是否出现（Boot OK）
3) 打开 Debug 面板：点击 Ping/ResolveAll/Config 预览
4) 在设置页面切换语言或开关 `VerboseLogs` 并保存 → 返回 Debug 面板点击 Reload → 日志出现 `Config Reloaded`

---

## 8. 附录

### 8.1 默认配置（内部 CoreConfig 建议结构）

```json
{
  "Version": "v5-P1",
  "General": {
    "Locale": "zh-Hans"
  },
  "Diagnostics": {
    "VerboseLogs": false
  },
  "UI": {
    "DebugPanelEnabled": true
  },
  "Prompt": {},
  "History": {},
  "Stage": {},
  "Orchestration": {},
  "Embedding": {}
}
```

说明：
- P1 仅消费 `General/Diagnostics/UI` 三处；其余节点为空壳，后续阶段逐步填充。

### 8.2 Debug 面板期望输出样例

- Ping：`[RimAI.P1] pong | services=3 | version=v5-P1`
- ResolveAll：
  - `OK IConfigurationService (constructed in 18 ms)`
  - `OK ServiceContainer (constructed in 2 ms)`
- Config Reloaded：`[RimAI.P1] Config Reloaded (version=v5-P1, locale=zh-Hans, at=2025-08-01T12:00:00Z)`

### 8.3 常见问题（FAQ）

- Q：为什么只暴露只读快照，不允许直接改配置？
  - A：保证一致性与可观测性；所有修改通过设置页→保存→Reload→事件广播，避免隐性状态漂移。
- Q：容器能否延迟构造以减少启动耗时？
  - A：可，但 P1 强制预热以尽早暴露 wiring 问题。后续阶段可对个别重服务做按需懒构造。
- Q：为什么禁止属性注入？
  - A：隐藏依赖、破坏可测试性与可观测性；统一构造函数注入可由容器在启动即校验完整依赖。

---

## 9. 变更记录（提交要求）

- 初版（v5-P1）：交付容器 + 配置 + Debug 三件套；冻结 `IConfigurationService`/`CoreConfigSnapshot`
- 后续修改：如需新增 Snapshot 字段，必须向后兼容并在本文“附录 8.1”同步更新默认值

---

本文件为 V5 P1 唯一权威实施说明。实现与验收以本文为准。
