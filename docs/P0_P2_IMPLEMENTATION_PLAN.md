# RimAI.Core V4 – P0~P2 详细实施方案

> 版本：v4.0.0-alpha  
> 更新日期：2025-08-04  
> 范围：完成 Skeleton（P0）、DI & Config（P1）、LLM Gateway（P2）三阶段，实现最小可运行闭环。

---

## 目录
1. 背景与目标  
2. 总里程碑与时间预估  
3. 阶段交付一览  
4. 详细任务拆解  
   4.1 P0 Skeleton  
   4.2 P1 DI & Config  
   4.3 P2 LLM Gateway  
5. Debug Panel 规范  
6. 验收标准  
7. 交付物清单

---

## 1. 背景与目标
本文件针对 **IMPLEMENTATION_V4.md** 中 P0~P2 的高阶描述，提供可直接执行的详细开发指引，确保在 3 天内实现：
* Mod 可加载且无红色报错；
* `ServiceContainer` 支持构造函数依赖注入；
* `IConfigurationService` 可热重载并通知订阅者；
* `ILLMService` 封装 `RimAIApi.GetCompletionAsync`，完成最小 LLM 闭环；
* Debug Panel 提供按钮 **Ping / Reload Config / Chat Echo** 用于端到端验证。

## 2. 总里程碑与时间预估
| 阶段 | 目标 MVP | 主要代码范围 | 预计工时 |
|------|----------|-------------|----------|
| P0 | Mod 可加载 + Ping | Lifecycle、Infrastructure | 0.5 天 |
| P1 | DI & Config | Infrastructure | 1 天 |
| P2 | LLM Gateway + Chat Echo | Modules/LLM、UI | 1.5 天 |
| **合计** | Skeleton→LLM 闭环 | — | **3 天** |

> *并行性：P1 的 DI 与 Config 可并行；P2 的 DebugPanel 按钮可与 LLMService 实现并行。*

## 3. 阶段交付一览
| 阶段 | 新增/修改文件 | 关键类 | DebugPanel 按钮 |
|------|---------------|--------|-----------------|
| P0 | `Lifecycle/RimAIMod.cs`  
`Infrastructure/ServiceContainer.cs`  
`Infrastructure/CoreServices.cs`  
`UI/DebugPanel/MainTabWindow_RimAIDebug.cs` | `ServiceContainer` (Init) | Ping |
| P1 | `Infrastructure/ConfigurationService.cs`  
`Settings/CoreConfig.cs` 等模型 | `IConfigurationService` | Reload Config |
| P2 | `Modules/LLM/ILLMService.cs`  
`Modules/LLM/LLMService.cs`  
DebugPanel 更新 | `LLMService` | Chat Echo |

## 4. 详细任务拆解

### 4.1 P0 Skeleton
| # | 任务 | 负责人 | 预计时长 |
|---|------|--------|----------|
| 0-1 | 创建 `RimAIMod` 入口，调用 `ServiceContainer.Init()` | — | 0.1d |
| 0-2 | `ServiceContainer` 基础实现：注册/解析单例（手动装配） | — | 0.2d |
| 0-3 | `CoreServices` 静态门面（受限场景使用） | — | 0.05d |
| 0-4 | DebugPanel 构建，按钮 **Ping**（`Messages.Message("RimAI Core Loaded")`） | — | 0.15d |

**完成判定**：游戏主菜单加载无红字；开发者模式点击 Ping 弹窗成功。

---

### 4.2 P1 DI & Config
#### A. DI 增强
1. 反射分析构造函数 → 递归解析依赖。  
2. 提供 `RegisterInstance<T>(obj)` 便于注入现成对象。  
3. 添加循环依赖检测（简单栈追踪）。

#### B. 配置系统
1. 数据模型
```csharp
public record CoreConfig
{
    public LLMConfig LLM { get; init; } = new();
    public CacheConfig Cache { get; init; } = new();
}
public record LLMConfig(double Temperature = 0.7, string ApiKey = "");
```
2. `ConfigurationService`
* 加载：从 `RimAIFrameworkSettings` 读取；若缺失用默认。  
* 事件：`event Action<CoreConfig> OnConfigurationChanged;`  
* `Reload()`：重新加载并触发事件。
3. 在 `ServiceContainer.Init()` 中注册单例。
4. DebugPanel 按钮 **Reload Config**：调用 `ConfigurationService.Reload()` 并打印温度值。

**完成判定**：修改 Mod 设置中的 Temperature，点击 Reload Config 按钮后日志显示新值。

---

### 4.3 P2 LLM Gateway
#### A. 服务契约与实现
| 接口 | 方法 |
|-------|------|
| `ILLMService` | `Task<string> GetResponseAsync(UnifiedChatRequest req, CancellationToken ct = default)` |

`LLMService` 行为：
1. 生成请求 → 调用 `RimAIApi.GetCompletionAsync(req)`；
2. 解析 `Result<UnifiedChatResponse>`：若成功返回 `Message.Content`，否则抛/返回错误；
3. 预留 `_cache`, `_retryPolicy` 字段，暂不实现逻辑；
4. 构造注入 `IConfigurationService` 以读取温度等参数。

注册：`ServiceContainer.Register<ILLMService, LLMService>();`

#### B. DebugPanel 按钮 **Chat Echo**
1. 预设 messages：system="You are helpful", user="Echo this"。  
2. 构造 `UnifiedChatRequest`; 调用 `ILLMService.GetResponseAsync`;  
3. 将返回文本追加到右侧多行文本框；  
4. 若 `Result.IsSuccess==false`，打印 `Error`。

**完成判定**：
* API Key 正确时，Chat Echo 输出模型返回内容；
* Key 缺失/错误时，控制台输出 `Result.Error` 字样且程序不中断。

---

## 5. Debug Panel 规范
| 区域 | 功能 |
|------|------|
| 左侧按钮列表 | 按阶段分组：`P0` Ping、`P1` Reload Config、`P2` Chat Echo；后续阶段依序追加 |
| 右侧日志框 | 上滚显示按钮执行日志和 AI 回复；限制行数防溢出 |
| 可见性 | 仅在 RimWorld 开启开发者模式 & 设置里勾选 "显示 RimAI Debug" 时显示 |

---

## 6. 验收标准
1. `core/v4.0.0-alpha` tag 下包含完整代码，能够通过 `msbuild /t:Build`。  
2. RimWorld 启动后 **无**红色 error log。  
3. DebugPanel 三按钮全部可正常执行：
   * Ping → 绿字信息框；
   * Reload Config → 温度值变化实时打印；
   * Chat Echo → 模型返回文本或错误信息，不崩溃。
4. 录屏演示以上流程并附到 PR。

---

## 7. 交付物清单
* 源码：`Lifecycle/`, `Infrastructure/`, `Modules/LLM/`, `UI/DebugPanel/` 等新增文件。  
* 此文档 `docs/P0_P2_IMPLEMENTATION_PLAN.md`。  
* 更新 `CHANGELOG.md`（新增 v4.0.0-alpha 条目）。  
* 测试录屏：`media/p0_p2_demo.mp4`（供审核）。

---

> **后续**：完成 P2 后，进入 P3 Scheduler & WorldAccess，届时将另行撰写 `P3_P4_IMPLEMENTATION_PLAN.md`。