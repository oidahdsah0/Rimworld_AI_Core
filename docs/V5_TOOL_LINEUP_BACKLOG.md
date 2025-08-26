# V5 工具推进目录（Backlog）

目标配额：
- 1 级工具：15 个（基础态势/只读汇总，面向玩家常见关心点）
- 2 级工具：10 个（进阶分析/预测/定位瓶颈）
- 3 级工具：5 个（策略建议/应急处置清单，仍保持只读与建议）

设计风格：参考「领地状况小助手」——返回结构化 JSON，字段清晰、可被外层直接消费，必要时提供简短的建议字段。

---

## Lv1（15）
1. get_colony_status（已实现）
   - 概览：人物清单、食物、药品、威胁四块
   - 输出：people[], food{}, medicine{}, threats{}
2. get_resource_overview（已实现）
   - 常见资源存量、日耗估算、缺口提示
   - 输出：resources[{defName, qty, dailyUse, daysLeft}]
3. get_power_status（已实现）
   - 发电/用电/电池电量/断电风险
   - 输出：power{gen, cons, net, batteries{count, stored, days}}
4. get_weather_status（已实现）
   - 时间+天气+风/降水+温度短期趋势+条件+建议（v1）
   - 输出：{time{}, weather{}, temp{now, seasonal, next[], min, max, trend}, conditions[], growth, enjoyableOutside, advisories[]}
5. get_storage_saturation（已实现）
   - 各仓库/重要存放点占用率，阻塞风险
   - 输出：storages[{name, usedPct, critical, notes}]
   - 备注：critical 阈值 0.85；按 Zone/Building/Group 估算容量；同名合并显示
6. get_research_options（已实现）
   - 可研究清单（当前可用+关键锁定项）：名称、描述、基础研究点、前置、所需工作台/科技印记、粗略所需时间
   - 输出：research{current{project, progressPct, workLeft, etaDays}, availableNow[{defName, label, desc, baseCost, techLevel, prereqs[], benches[], techprintsNeeded, roughTimeDays}], lockedKey[{defName, label, missingPrereqs[], benchesMissing[], techprintsMissing, note}], colony{researchers, effectiveSpeed}}
   - 备注：roughTimeDays 基于“当前有效研究速度”粗估；availableNow 默认限 TopN=12 便于上游提示词消费
7. get_construction_backlog（已实现）
   - 待建蓝图、材料缺口、阻塞点
   - 输出：builds[{thing, count, missing[{res, qty}]}]
   - 备注：扫描当前地图蓝图/框架；按 DefName/Label 归并并聚合缺口；基于 resourceCounter 粗估，不含在途/容器专用投喂。
8. get_security_posture（已实现）
   - 炮塔/陷阱/火力覆盖与缺口
   - 输出：security{turrets[{type,label,x,z,range,minRange,losRequired,flyOverhead,dpsScore,powered,manned,holdFire}], traps[{type,label,x,z,resettable}], coverage{areaPct,strongPct,avgStack,overheadPct,approaches[{entryX,entryZ,avgFire,maxGapLen,trapDensity}]}, gaps[{centerX,centerZ,minX,minZ,maxX,maxZ,area,distToCore,reason}], note}
   - 备注：无炮塔时 note 提示英文说明；领地优先使用家区，回退为殖民建筑扩张；覆盖计算考虑 LOS 与抛射。 
9. get_mood_risk_overview（已实现）
   - 心情分布、即将崩溃人数与主因
   - 输出：mood{avgPct, minorCount, majorCount, extremeCount, nearBreakCount, topCauses[{label, totalImpact, pawnsAffected}]}
   - 备注：思潮聚合使用 ThoughtHandler.GetDistinctMoodThoughtGroups + MoodOffsetOfGroup，确保与 UI 逻辑一致；仅汇总负面项（offset<0）。
10. get_medical_overview（健康检查 v1）
   - 殖民地健康总览（优先输出汇总）：健康均值/疼痛均值、需包扎数、出血/感染/生命危险人数、手术计划数 + 轻量风险评分与提示
   - 输出（汇总优先）：medical{summary{totalColonists, patientsNeedingTend, bleedingCount, infectionCount, operationsPending, lifeThreatCount, avgHealthPct, avgPainPct, riskScore}, groups{bleeding[], infections[], operations[], lifeThreats[]}, pawns[], advisories[]}
   - 备注：v1 先提供 overall/hediffs；capacities/parts 可在 v1.1 开关展开；按 Thought/Health UI 口径计算 healthPct/pain/bleeding/感染免疫。
11. get_wildlife_opportunities（已实现 v1）
   - 野生动物按物种聚合：数量、风险（捕食/群居/复仇/爆炸）与收益（肉/皮革）+ 简短建议
   - 输出：wildlife[{species, defName, count, predator, herdAnimal, packAnimal, insect, explosive, manhunterOnDamageChance, avgBodySize, avgWildness, meatPer, leatherPer, leatherDef, totalMeat, totalLeather, seasonOk, suggestedApproach, notes[]}]
12. get_trade_readiness（已实现 v1）
   - 可交易银币、信标覆盖/电力、通讯台可用性，以及信标覆盖范围内可交易物资清单
   - 输出：trade{silver, beacons{total, powered, coverageCells, inRangeStacks}, comms{hasConsole, usableNow}, goods[{defName, label, qty, totalValue}]}
13. get_animal_management（已实现 v1）
   - 牲畜/战兽数量、训练、口粮压力
   - 输出：animals{counts{total, species[{defName,label,count}]}, training{obedience{eligible,learned}, release{...}, rescue{...}, haul{...}}, food{totalNutrition, dailyNeed, days, sources[{defName,label,count,nutritionPer,totalNutrition}]}}
14. get_prison_overview（已实现 v1）
   - 囚犯概况、叛乱/逃狱风险、招募点
   - 输出：prison{count, recruitables[{pawn,pawnLoadId,mode}], risks[]}
15. get_alert_digest（已实现 v1）
   - 当前 RimWorld 警报聚合与严重度排序
   - 输出：alerts[{id, label, severity, hint}]

## Lv2（10）
1. get_raid_readiness（进行中 v1：威胁点与规模估算）
   - 当前财富/人口/战兽/机仆构成与 Storyteller 因子，估算 DefaultThreatPointsNow 与袭击规模区间，分级风险带
   - 输出：raid{wealth{total,items,buildings,pawns,playerWealthForStoryteller}, colony{humanCount,armedCount,avgHealthPct}, animals{battleReadyCount,pointsContribution}, mechs{count,combatPowerSum}, points{finalPoints,difficultyScale,adaptationApplied,timeFactor,randomFactorMin,randomFactorMax,daysSinceSettle}, riskBand, sizes[{archetype,min,max}]}
3. get_workload_balance
   - 工作分配热点、空闲与瓶颈
   - 输出：workload{idlePct, overloadAreas[], suggestions[]}
4. get_pathing_blockers
   - 关键通道阻塞/地形减速（雪/泥/瓦砾）
   - 输出：pathing{blockers[], slowTiles[], fixes[]}
5. get_fire_safety
   - 可燃物密度、灭火覆盖、家区设置
   - 输出：fire{hotspots[], coverage, homeAreaIssues[]}
6. get_cleanliness_risk
   - 医疗房/厨房清洁度与食物中毒风险
   - 输出：cleanliness{rooms[], risk}
7. get_infestation_risk
   - 头顶山体热力图、温度/电力因素
   - 输出：infestation{hotZones[], score}
8. get_mood_causality
   - 全局主因分析与对策
   - 输出：moodCausality{topThoughts[], actions[]}
9. get_storage_optimizer
   - 再堆叠/重分区建议，减少搬运浪费
   - 输出：storageOpt{moves[], impact}
10. get_schedule_health
    - 作息遵循度、疲劳与效率
    - 输出：schedule{compliance, issues[], tips[]}

## Lv3（5）
1. get_emergency_triage
   - 即时应急清单（救援、止血、灭火、断电）
   - 输出：triage[{priority, action, target, why}]
2. get_base_defense_plan
   - 防御构筑建议（沙袋/炮塔/陷阱/门）
   - 输出：defense{placements[], matCost}
3. get_production_plan
   - 生产线/配方/原料供应的效率优化
   - 输出：production{bills[], bottlenecks[], gains}
4. get_caravan_readiness_plan
   - 商队打包清单与缺口
   - 输出：caravan{items[], animals[], days, gaps[]}
5. get_disaster_scenarios
   - 断电/冷潮/毒雨等预案与短板
   - 输出：scenarios[{type, risk, prep[], gaps[]}]

---

实现节奏建议：
- 先从 Lv1 中挑 4-6 个“高频只读概览”并行推进（与现有 Parts 高复用），确保统一 JSON 规范与 Tool 注册元数据。
- 逐步补充 Lv2 的分析工具，沉淀通用预测/评分模块。
- 最后实现 Lv3 建议型工具，侧重可操作条目与材料清单，但保持只读输出。
