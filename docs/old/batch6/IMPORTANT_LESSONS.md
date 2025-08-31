# 重要经验 — StatPart + Hediff 运行时注入（RimWorld 1.6）

本文总结本次「AI 服务器 Buff」从“不生效/看起来生效”到“确实修改系统数值”的全过程：问题、根因、解决方案、结果结论与最佳实践。

## 问题概述

- 目标：AI 服务器建筑应当实打实提升殖民者的“工作类”属性（系统级数值，而非仅 UI 文案）。
- 现象：早期用 XML Patch 注入 StatPart 的方案，经常出现“UI 有描述，但数值没变”的情况。
- 要求：健康面板需有绿色 Hediff 作为可视提示，但不要污染 Inspect 字符串。

## 根因分析

1) XML Patch 脆弱与加载次序差异
- 通过 XML 注入 StatPart 在 1.6 环境下并不稳定，某些场景下 Schema/补丁错误或加载顺序导致 StatDef.parts 未包含我们的 StatPart。
- 结果：`TransformValue/ExplanationPart` 从未执行，UI 文案再多也只是“看起来生效”。

2) 运行时创建 StatPart 时未设置 parentStat
- 刚转为运行时注入时，忘记了 `part.parentStat = statDef`。
- 没有 parentStat，RimWorld 的 Stat 管道不会正确调用我们的逻辑；解释行不出现，数值也不变。

3) 信号来源不唯一导致抖动
- 一度同时使用“直接扫描服务器”与“未挂 Hediff 的临时逻辑”，会在边界时刻出现轻微抖动与歧义；需要一个单一事实源。

## 最终方案

- Hediff 作为单一事实源：
	- 新增可见的绿色 Hediff（`RimAI_ServerBuff`），其 Severity 表示全局加成百分比。
- MapComponent 维护严重度：
	- 每 ~1 秒（60 ticks）扫描地图上在线的 AI 服务器，计算百分比后，为所有玩家阵营的人形单位设置/更新 Hediff 严重度；为 0 时移除 Hediff。
- StatPart 消费 Hediff：
	- `StatPart_RimAIServer` 从 Hediff.Severity 读取加成，对工作相关 Stat 乘法增益；若瞬时缺失 Hediff，仅做轻量回退扫描避免偶发空窗。
- 运行时注入：
	- `StatPartRuntimeInjector` 在启动期将 `StatPart_RimAIServer` 注入相关 StatDef，并“必须”设置 `part.parentStat = statDef`。
	- DevMode 下仅在启动时打印一次“已附着的 StatDef 列表”。
- 去除 Inspect 后缀：
	- 为避免 UI 噪音，不再给 Inspect 字符串打补丁；健康面板通过 HediffComp 在括号内显示“+X%”。

## 验证与结论

- 重启游戏后（触发静态构造）：相关工作类 Stat 数值确实提高，且在该 Stat 的解释中能看到一行：`AI服务器增益 severity: +X%`。
- DevMode 启动日志显示已附着的 StatDef 名单，便于一次性确认注入是否成功。
- 结论：这是“系统级真实数值”变化，而非 UI 文案；方案稳定、可重复，并且 UI 噪音可控。

## 最佳实践（沉淀）

- StatPart 注入（1.6）：
	- 优先使用“运行时注入（StaticConstructorOnStartup）”，避免 XML Patch 脆弱性。
	- 运行时创建 StatPart 时“必须”设置 `parentStat`。
- 单一事实源：
	- 用 Hediff.Severity 统一驱动“效果”和“显示”；必要时提供极短回退，稳定态只依赖 Hediff。
- UI 与可观测：
	- 健康面板可视化优先；避免改写 Inspect 文本。
	- 解释行风格与原版一致：`<hediffLabel> severity: +X%`。
- Tick 与性能：
	- MapComponent 巡检 ≥ 60 ticks；仅更新必要对象，避免多次全量遍历。
- 日志纪律：
	- 高频路径默认静音；诊断日志受 `Prefs.DevMode` 控制。
	- 启动期可打印一次“注入清单”，其余日志最小化。

## 附录：影响的工作类 Stat 列表

- WorkSpeedGlobal、GlobalWorkSpeed、GeneralLaborSpeed、ConstructionSpeed、MiningSpeed、PlantWorkSpeed、ResearchSpeed、MedicalOperationSpeed、MedicalTendSpeed；若存在兼容别名则一并注入。

