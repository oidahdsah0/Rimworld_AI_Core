# V5 工具推进目录（Backlog）

目标配额：
- 1 级工具：15 个（基础态势/只读汇总，面向玩家常见关心点）
- 2 级工具：10 个（进阶分析/预测/定位瓶颈）
- 3 级工具：5 个（策略建议/应急处置清单，仍保持只读与建议）

设计风格：参考「领地状况小助手」——返回结构化 JSON，字段清晰、可被外层直接消费，必要时提供简短的建议字段。

---

## Lv1（12）
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

## Lv2（6）
13. get_animal_management（已实现 v1）
   - 牲畜/战兽数量、训练、口粮压力
   - 输出：animals{counts{total, species[{defName,label,count}]}, training{obedience{eligible,learned}, release{...}, rescue{...}, haul{...}}, food{totalNutrition, dailyNeed, days, sources[{defName,label,count,nutritionPer,totalNutrition}]}}
14. get_prison_overview（已实现 v1）
   - 囚犯概况、叛乱/逃狱风险、招募点
   - 输出：prison{count, recruitables[{pawn,pawnLoadId,mode}], risks[]}
15. get_alert_digest（已实现 v1）
   - 当前 RimWorld 警报聚合与严重度排序
   - 输出：alerts[{id, label, severity, hint}]
16. get_raid_readiness（进行中 v1：威胁点与规模估算）
   - 当前财富/人口/战兽/机仆构成与 Storyteller 因子，估算 DefaultThreatPointsNow 与袭击规模区间，分级风险带
   - 输出：raid{wealth{total,items,buildings,pawns,playerWealthForStoryteller}, colony{humanCount,armedCount,avgHealthPct}, animals{battleReadyCount,pointsContribution}, mechs{count,combatPowerSum}, points{finalPoints,difficultyScale,adaptationApplied,timeFactor,randomFactorMin,randomFactorMax,daysSinceSettle}, riskBand, sizes[{archetype,min,max}]}
17. ai_diplomat（已实现 v1）
   - 在巡检/命令执行时，若有通电 AI 终端且满足研究门槛，随机选择一个可进行外交的派系并调整好感度（-5..+15），用于周期性微调关系
   - 输出：{ ok, faction{ id, name, defName }, goodwill_before, delta, goodwill_after, note }
   - 备注：等级校验基于调用方传入的 server_level（需 ≥2）；运行时自检包括研究“RimAI_AI_Level2”与通电终端；派系清单取自世界服务（排除隐藏/永久敌对）
18. ai_orbital_bombardment（已实现 v1）
   - 旧卫星火炮破解：在敌对目标附近随机位置执行 5–15 次多类型爆炸（开发者式 Explosion），用于紧急火力支援；命令模式触发；触发前需装载在服务器工具槽
   - 输入：server_level（可选，1..3，调用方注入），radius（默认9），max_strikes（默认9，范围5–15）
   - 输出：成功 { ok:true, strikes_executed, radius, cooldown_days:3 }；失败 { ok:false, error, seconds_left? }
   - 备注：Lv2 工具；设备门槛为通电 AI 终端；无敌对则拒绝并提示；执行开始/结束均有提示；冷却 3 天；巡检仅返回冷却与引导“可在命令模式触发”

## Lv3（3）
19. get_unknown_civ_contact（已实现 v1）
   - 研究完成后出现在 Lv3 工具下拉；选择/执行需已供电的“引力波天线”，未满足时给出本地化提示
   - 输出：{cipher_message, favor_delta, favor_total, cooldown_seconds, gift_triggered, gift_note}
   - 行为：好感变动范围 -5..+15；当 favor_total>65 且冷却到期，触发来自未知文明的赠礼（资源投放，数量系数 2.0），并在顶栏提示；赠礼冷却 3–5 天；工具本身只读（P4），落地写入由 WorldActionService 在主线程调度（P3）
20. set_forced_weather（已实现 v1）
   - 在当前地图强制指定天气 1–3 天；需 Lv3 服务器、通讯研究完成、天线通电；操作有 5 天冷却
   - 输入：weather_name（从枚举中模糊匹配），map_id（可选）
   - 输出：成功 { ok:true, weather, duration_days, cooldown_days }；失败 { ok:false, error, ... }
   - 规则：允许天气枚举（Clear, Fog, Rain, DryThunderstorm, RainyThunderstorm, FoggyRain, SnowHard, SnowGentle）；模糊匹配阈值 0.65；开始/结束会在左上角显示本地化提示；实际改动通过 WAS 在主线程施加 GameCondition，并记录冷却到持久化
21. invoke_subspace_entity（已实现 v1）
    - 触发一次“亚空间回声显化”；编排可对强烈“召唤词”直连命中；内部根据 llm_score(0–100) 计算强度分层与敌对组成
    - 输入：llm_score（0–100）
    - 输出：成功 { ok:true, tier, composition, count, cooldown_days }；失败 { ok:false, error, ... }
    - 规则：
       - 准入：需 Lv3 服务器、研究“亚空间引力波穿透”(RimAI_Subspace_Gravitic_Penetration) 完成、引力波天线通电
       - 冷却：2 天；将最近一次与下次可用写入持久化（SubspaceInvocationState）
       - 组成优先：徘徊者(Anomaly: Revenant)/僵尸(Shambler)；缺失则回退虫群；若存在对应 Incident（如 Revenant）则优先用 Incident 触发，否则直接在主线程生成敌对单位
       - 强度分层：low/mid/high/apex（由 llm_score 划分）
       - 提示：开始时在左上角显示本地化提示；触发方式与天气控制器相似，均通过 WAS 在主线程执行，失败快速返回
