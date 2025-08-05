# RimAI.Core V4 – P0~P2 详细实施方案

> 版本：v4.0.0-alpha  
> 更新日期：2025-08-05  
> 范围：Skeleton（P0）、DI & Config（P1）、LLM Gateway（P2）  
> 本文与 `ARCHITECTURE_V4.md` / `IMPLEMENTATION_V4.md` 同步，任何字段变更需同时更新。

---

## 1. 背景与目标

在 v4 的“小步快跑”策略下，P0~P2 要完成 **端到端最小闭环**：Mod 成功加载 → DI 初始化 → 配置热重载 → LLM Echo。  
**运行时兼容性前置条件：所有代码仅可使用 .NET Framework 4.7.2 可用 API**；若引用高版本特性将直接拒绝合并。

核心交付：
1. RimAIMod 入口 + ServiceContainer 初版。
2. ConfigurationService（强类型配置 + `Reload()` 事件）。
3. LLMService（封装 `RimAIApi.GetCompletionAsync`，含缓存与简单重试）。
4. DebugPanel：Ping / Reload Config / Chat Echo + LLM 五项测试（流式 / 非流式 / JSON / Tools / 批量）。

---

## 2. 里程碑与时间预估

| 阶段 | 目标 MVP | Debug 面板按钮 | 预计工时 |
|------|----------|----------------|----------|
| **P0** | Mod 可加载 + Ping | Ping | 0.5 天 |
| **P1** | DI & 配置热重载 | Reload Config | 1 天 |
| **P2** | LLM Echo（含缓存计数 + 流式/非流式/JSON/Tools/批量 测试） | Chat Echo + LLM Tests | 1.5 天 |
| **合计** | — | — | **3 天** |

---

## 3. Gate 验收清单

| 阶段 | Gate 条件（全部满足才可合并主干） |
|------|-----------------------------------|
| **P0** | ① RimAIMod 日志打印 `RimAI v4 Skeleton Loaded` ② `CoreServices.ServiceContainer` 非 null ③ RimWorld 主菜单无红色报错 |
| **P1** | ① 构造函数 DI 解析通过 ② 调用 `Reload()` 事件触发并打印新温度 ③ DebugPanel 按钮显示最新配置 |
| **P2** | ① `GetResponseAsync("hello")` 返回 "hello" ② DebugPanel 显示**缓存命中率**与**重试日志(≤3)** ③ LLM 请求耗时 < 3s (Mock) ④ DebugPanel 提供 流式 / 非流式 / JSON / Tools / 批量 请求测试全部通过 |

每阶段合并时需附带 Gate 录像，放置于 `media/` 目录。

---

## 4. 阶段交付一览

| 阶段 | 新增/修改文件 | 关键类 | DebugPanel 按钮 |
|------|---------------|--------|-----------------|
| P0 | `Lifecycle/RimAIMod.cs` `Infrastructure/ServiceContainer.cs` `Infrastructure/CoreServices.cs` `UI/DebugPanel/MainTabWindow_RimAIDebug.cs` | `ServiceContainer` (Init) | Ping |
| P1 | `Infrastructure/ConfigurationService.cs` `Settings/CoreConfig.cs` 等 | `IConfigurationService` | Reload Config |
| P2 | `Modules/LLM/ILLMService.cs` `Modules/LLM/LLMService.cs` + Panel 更新 | `LLMService` | Chat Echo |

---

## 5. 详细任务拆解

### 5.1 P0 Skeleton
1. 创建 `RimAIMod`，在 `OnLoaded` 调用 `ServiceContainer.Init()`。
2. `ServiceContainer` 实现单例注册 & 手动解析（后续增强）。
3. `CoreServices` 提供静态门面，Ping 按钮内部调用 `CoreServices.Logger.Info()`。
4. DebugPanel 初版：左侧按钮区 + 右侧日志区；实现 **Ping**。

### 5.2 P1 DI & Config
A. **DI 增强**
* 反射构造函数、递归解析、循环依赖检测。
* `RegisterInstance<T>` 支持测试注入。

B. **配置系统**
* 定义 `CoreConfig` / `LLMConfig` / `CacheConfig`。默认值来自常量。
* `ConfigurationService` 加载 RimWorld 设置；`Reload()` 触发 `OnConfigurationChanged`。
* DebugPanel 按钮 **Reload Config**：重载后打印 `Current.LLM.Temperature`。

### 5.3 P2 LLM Gateway
* 定义 `ILLMService`：`GetResponseAsync` (P2) + `StreamResponseAsync` (stub)。
* `LLMService` 行为：调用 `RimAIApi`; 包装 `Result<T>`; 缓存键 = SHA256(request)。
* 内置 `RetryPolicy`: 指数退避 1s/2s/4s；在 DebugPanel 输出 `[Retry #]`。
* 缓存：首次 Miss → 调用 API; Hit → 直接返回并统计 `CacheHits`。
* DebugPanel 按钮：
  * **Chat Echo**（非流式）
  * **LLM Stream Test**（流式）
  * **LLM JSON Test**（JSON 模式）
  * **LLM Tools Test**（工具调用）
  * **LLM Batch Test**（批量请求）
  所有按钮均需在日志中输出 `Response`、`Retries`、`CacheHits`（若适用）

---

## 6. Debug Panel 规范
同 `ARCHITECTURE_V4.md`：左侧按钮按阶段分组；右侧日志保留 100 行滚动；面板仅在开发者模式 + 设置勾选时可见。

---

## 7. 交付物清单
* 源码：`Lifecycle/`, `Infrastructure/`, `Modules/LLM/`, `Settings/`, `UI/DebugPanel/`。
* 文档：本文件、`IMPLEMENTATION_V4.md`, `ARCHITECTURE_V4.md` 更新。
* 录像：`media/p0_p2_demo.mp4`。
* CHANGELOG：新增 `v4.0.0-alpha`。

---

## 8. TODO Checklist (快速视图)

### P0 Skeleton
- [ ] RimAIMod 入口 + 日志
- [ ] ServiceContainer 手动注册
- [ ] CoreServices 静态门面
- [ ] DebugPanel + Ping

### P1 DI & Config
- [ ] 反射构造解析 & 循环依赖
- [ ] RegisterInstance
- [ ] CoreConfig/LLMConfig/CacheConfig 定义
- [ ] ConfigurationService + Reload() 事件
- [ ] DebugPanel Reload Config

### P2 LLM Gateway
- [ ] ILLMService 接口
- [ ] LLMService 实现 + 缓存 + 重试
- [ ] ServiceContainer 注册单例
- [ ] DebugPanel LLM Tests（Chat Echo / Stream / JSON / Tools / Batch，展示缓存/重试）
- [ ] 更新 CHANGELOG & 录像
