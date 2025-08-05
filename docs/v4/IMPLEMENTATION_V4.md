# RimAI.Core V4 逐步实施计划（IMPLEMENTATION_V4.md）

> 更新日期：2025-08-05  
> 所有人务必在开始开发前阅读本表。

## 阶段表

| 阶段 | 目标 MVP | 主要交付代码 | Debug 面板按钮 | 预计工期 |
|------|----------|-------------|----------------|----------|
| P0 Skeleton | Mod 可加载，DebugPanel 弹窗 | RimAIMod 空壳 + ServiceContainer Init | Ping | 0.5 天 |
| P1 DI & Config | IConfigurationService + 热重载 | Reload Config | 1 天 |
| P2 LLM Gateway | ILLMService(min) + GetCompletionAsync | Chat Echo | 2 天 |
| P3 Scheduler+WorldData | ISchedulerService + GetPlayerName | Get Player Name | 2 天 |
| P4 Tool System | ToolRegistry + GetColonyStatusTool | Run Tool | 2 天 |
| P5 Orchestration(min) | ExecuteToolAssistedQueryAsync | Ask Colony Status | 3 天 |
| P6 Persistence | History 保存/加载 | Record History | 2 天 |
| P7 Event Aggregator | 聚合伤病事件 | List Aggregated Events | 2 天 |
| P8 Persona & Stream UI | Persona + StreamResponseAsync UI | Chat with Assistant | 3 天 |

> 说明：  
> • 每阶段结束均需提交录像证明所有按钮通过。  
> • Contracts 子目录一旦合并到主分支即视为冻结。  
> • 若实际工期偏差 >20%，需在周会上重新估算。

## 里程碑 & 版本号策略

| 版本 | 内容 | 对应 Tag |
|------|------|---------|
| v4.0.0-alpha | 完成 P0~P2，可对话但未写入游戏 | core/v4.0.0-alpha |
| v4.0.0-beta  | 完成 P0~P5，工具辅助查询完整 | core/v4.0.0-beta |
| v4.0.0       | 完成 P0~P8，公开 Steam 预览版 | core/v4.0.0 |


## 进度日志 (Progress Log)

| 日期 | 状态 |
|------|------|
| 2025-08-05 | P0~P2 已完成并合并，生成 core/v4.0.0-alpha。Ping / Reload Config / Chat Echo 按钮全部通过。 |

---

后续若阶段新增，请在此表追加行，不得覆盖历史记录。
