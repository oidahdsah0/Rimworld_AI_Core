using System;
using System.Collections.Generic;
using System.Linq;
using RimAI.Core.Architecture.Interfaces;
using RimWorld;
using Verse;

namespace RimAI.Core.Analysis
{
    /// <summary>
    /// 殖民地分析器实现 - 负责收集和分析殖民地状态
    /// </summary>
    public class ColonyAnalyzer : IColonyAnalyzer
    {
        private static ColonyAnalyzer _instance;
        public static ColonyAnalyzer Instance => _instance ??= new ColonyAnalyzer();

        private ColonyAnalyzer() { }

        public ColonyStatus AnalyzeCurrentStatus()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null)
                {
                    Log.Warning("[ColonyAnalyzer] No current map available");
                    return CreateEmptyStatus();
                }

                var status = new ColonyStatus
                {
                    ColonistCount = map.mapPawns.FreeColonistsCount,
                    ResourceSummary = GenerateResourceSummary(map),
                    ThreatLevel = EvaluateThreatLevel(map),
                    ActiveEvents = GetActiveEvents(),
                    WeatherCondition = GetWeatherDescription(map),
                    Season = GetSeasonDescription(map),
                    ResourceLevels = GetResourceLevels(map),
                    Colonists = GetColonistInfo(map),
                    LastUpdated = DateTime.Now
                };

                return status;
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to analyze colony status: {ex.Message}");
                return CreateEmptyStatus();
            }
        }

        public List<ThreatInfo> IdentifyThreats()
        {
            var threats = new List<ThreatInfo>();
            
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return threats;

                // 检查敌对派系
                var hostileFactions = Find.FactionManager.AllFactions
                    .Where(f => f.HostileTo(Faction.OfPlayer))
                    .ToList();

                foreach (var faction in hostileFactions)
                {
                    threats.Add(new ThreatInfo
                    {
                        Type = "敌对派系",
                        Level = faction.def.permanentEnemy ? ThreatLevel.High : ThreatLevel.Medium,
                        Description = $"派系 '{faction.Name}' 处于敌对状态",
                        DetectedAt = DateTime.Now,
                        IsActive = true,
                        Details = new Dictionary<string, object>
                        {
                            ["FactionName"] = faction.Name,
                            ["FactionType"] = faction.def.defName,
                            ["Goodwill"] = faction.GoodwillWith(Faction.OfPlayer)
                        }
                    });
                }

                // 检查活跃的威胁事件
                var storyteller = Find.Storyteller;
                if (storyteller?.AllIncidents != null)
                {
                    var recentIncidents = storyteller.AllIncidents
                        .Where(i => i.def.category == IncidentCategoryDefOf.ThreatBig || 
                                   i.def.category == IncidentCategoryDefOf.ThreatSmall)
                        .Take(5);

                    foreach (var incident in recentIncidents)
                    {
                        threats.Add(new ThreatInfo
                        {
                            Type = "故事事件",
                            Level = incident.def.category == IncidentCategoryDefOf.ThreatBig ? 
                                   ThreatLevel.High : ThreatLevel.Medium,
                            Description = incident.def.label,
                            DetectedAt = DateTime.Now,
                            IsActive = true
                        });
                    }
                }

                // 检查资源短缺威胁
                var resourceThreats = AnalyzeResourceThreats(map);
                threats.AddRange(resourceThreats);

                // 检查殖民者健康威胁
                var healthThreats = AnalyzeHealthThreats(map);
                threats.AddRange(healthThreats);

            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to identify threats: {ex.Message}");
            }

            return threats;
        }

        public ResourceReport GenerateResourceReport()
        {
            var report = new ResourceReport();
            
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return report;

                // 分析关键资源
                var resourceDefs = new[]
                {
                    ThingDefOf.Silver,
                    ThingDefOf.WoodLog,
                    ThingDefOf.Steel,
                    ThingDefOf.Plasteel,
                    ThingDefOf.Gold,
                    ThingDefOf.Uranium
                };

                foreach (var resourceDef in resourceDefs)
                {
                    var count = map.resourceCounter.GetCount(resourceDef);
                    var status = new ResourceStatus
                    {
                        Name = resourceDef.label,
                        Current = count,
                        Maximum = float.MaxValue, // 大多数资源没有最大值限制
                        DailyChange = 0, // 需要历史数据计算
                        Priority = DetermineResourcePriority(resourceDef, count),
                        Status = DetermineResourceStatus(resourceDef, count)
                    };

                    report.Resources[resourceDef.defName] = status;

                    // 判断短缺和过剩
                    if (status.Priority == ResourcePriority.Critical)
                    {
                        report.CriticalShortages.Add(resourceDef.label);
                    }
                    else if (count > 5000) // 假设的过剩阈值
                    {
                        report.Surpluses.Add(resourceDef.label);
                    }
                }

                // 食物分析
                AnalyzeFoodSituation(map, report);

                report.OverallStatus = DetermineOverallResourceStatus(report);
                
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to generate resource report: {ex.Message}");
                report.OverallStatus = "资源分析失败";
            }

            return report;
        }

        public List<string> GetActiveEvents()
        {
            var events = new List<string>();
            
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return events;

                // 获取当前天气事件
                var weather = map.weatherManager.curWeather;
                if (weather != WeatherDefOf.Clear)
                {
                    events.Add($"天气: {weather.label}");
                }

                // 获取活跃的心情事件
                var colonists = map.mapPawns.FreeColonists;
                var moodEvents = new HashSet<string>();
                
                foreach (var colonist in colonists)
                {
                    if (colonist.needs?.mood?.thoughts != null)
                    {
                        var recentThoughts = colonist.needs.mood.thoughts.DistinctMemoryThoughts
                            .Take(3);
                        
                        foreach (var thought in recentThoughts)
                        {
                            if (thought.MoodOffset() != 0)
                            {
                                moodEvents.Add($"心情影响: {thought.LabelCap}");
                            }
                        }
                    }
                }
                
                events.AddRange(moodEvents.Take(5)); // 限制数量

            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to get active events: {ex.Message}");
                events.Add("事件获取失败");
            }

            return events;
        }

        public string GetColonyOverview()
        {
            try
            {
                var status = AnalyzeCurrentStatus();
                var threats = IdentifyThreats();
                var resources = GenerateResourceReport();

                var overview = $@"【殖民地概况】
殖民者数量: {status.ColonistCount}
威胁等级: {GetThreatLevelDescription(status.ThreatLevel)}
天气状况: {status.WeatherCondition}
当前季节: {status.Season}

【资源状况】
{resources.OverallStatus}
关键短缺: {(resources.CriticalShortages.Count > 0 ? string.Join(", ", resources.CriticalShortages) : "无")}

【活跃威胁】
{(threats.Count > 0 ? string.Join("\n", threats.Take(3).Select(t => $"- {t.Description}")) : "暂无重大威胁")}

【近期事件】
{(status.ActiveEvents.Count > 0 ? string.Join("\n", status.ActiveEvents.Take(3).Select(e => $"- {e}")) : "无特殊事件")}";

                return overview;
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to generate colony overview: {ex.Message}");
                return "殖民地概况生成失败";
            }
        }

        #region 私有辅助方法

        private ColonyStatus CreateEmptyStatus()
        {
            return new ColonyStatus
            {
                ColonistCount = 0,
                ResourceSummary = "无法获取资源信息",
                ThreatLevel = ThreatLevel.None,
                ActiveEvents = new List<string> { "数据不可用" },
                WeatherCondition = "未知",
                Season = "未知",
                LastUpdated = DateTime.Now
            };
        }

        private string GenerateResourceSummary(Map map)
        {
            try
            {
                var silver = map.resourceCounter.GetCount(ThingDefOf.Silver);
                var food = GetFoodCount(map);
                var steel = map.resourceCounter.GetCount(ThingDefOf.Steel);

                if (silver < 100 || food < 50 || steel < 50)
                {
                    return "资源紧张";
                }
                else if (silver > 1000 && food > 200 && steel > 200)
                {
                    return "资源充足";
                }
                else
                {
                    return "资源一般";
                }
            }
            catch
            {
                return "资源状态未知";
            }
        }

        private ThreatLevel EvaluateThreatLevel(Map map)
        {
            try
            {
                var threats = 0;

                // 检查敌对派系
                var hostileFactions = Find.FactionManager.AllFactions
                    .Count(f => f.HostileTo(Faction.OfPlayer));
                threats += hostileFactions;

                // 检查殖民者健康状况
                var colonists = map.mapPawns.FreeColonists;
                var injuredCount = colonists.Count(c => c.health.HasHediffsNeedingTend());
                if (injuredCount > colonists.Count() / 2) threats++;

                // 检查食物短缺
                if (GetFoodCount(map) < colonists.Count() * 10) threats++;

                return threats switch
                {
                    0 => ThreatLevel.None,
                    1 => ThreatLevel.Low,
                    2 => ThreatLevel.Medium,
                    3 => ThreatLevel.High,
                    _ => ThreatLevel.Critical
                };
            }
            catch
            {
                return ThreatLevel.None;
            }
        }

        private int GetFoodCount(Map map)
        {
            return map.resourceCounter.AllCountedAmounts
                .Where(kvp => kvp.Key.IsFood)
                .Sum(kvp => kvp.Value);
        }

        private string GetWeatherDescription(Map map)
        {
            try
            {
                return map.weatherManager.curWeather.label;
            }
            catch
            {
                return "天气未知";
            }
        }

        private string GetSeasonDescription(Map map)
        {
            try
            {
                return GenDate.Season(Find.TickManager.TicksAbs, map.Tile).ToString();
            }
            catch
            {
                return "季节未知";
            }
        }

        private Dictionary<string, float> GetResourceLevels(Map map)
        {
            var levels = new Dictionary<string, float>();
            
            try
            {
                levels["Silver"] = map.resourceCounter.GetCount(ThingDefOf.Silver);
                levels["Steel"] = map.resourceCounter.GetCount(ThingDefOf.Steel);
                levels["Wood"] = map.resourceCounter.GetCount(ThingDefOf.WoodLog);
                levels["Food"] = GetFoodCount(map);
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to get resource levels: {ex.Message}");
            }

            return levels;
        }

        private List<ColonistInfo> GetColonistInfo(Map map)
        {
            var colonistInfo = new List<ColonistInfo>();
            
            try
            {
                var colonists = map.mapPawns.FreeColonists;
                
                foreach (var colonist in colonists.Take(10)) // 限制数量避免过多数据
                {
                    var info = new ColonistInfo
                    {
                        Name = colonist.Name.ToStringShort,
                        Profession = colonist.story?.TitleCap ?? "未知",
                        Skills = GetTopSkills(colonist),
                        HealthStatus = GetHealthStatus(colonist),
                        MoodStatus = GetMoodStatus(colonist),
                        CurrentTask = GetCurrentTask(colonist),
                        IsAvailable = !colonist.Downed && !colonist.InBed()
                    };
                    
                    colonistInfo.Add(info);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to get colonist info: {ex.Message}");
            }

            return colonistInfo;
        }

        private List<string> GetTopSkills(Pawn colonist)
        {
            var skills = new List<string>();
            
            try
            {
                if (colonist.skills != null)
                {
                    var topSkills = colonist.skills.skills
                        .Where(s => s.Level > 5)
                        .OrderByDescending(s => s.Level)
                        .Take(3);
                    
                    foreach (var skill in topSkills)
                    {
                        skills.Add($"{skill.def.skillLabel} ({skill.Level})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[ColonyAnalyzer] Failed to get skills for {colonist.Name}: {ex.Message}");
            }

            return skills;
        }

        private string GetHealthStatus(Pawn colonist)
        {
            try
            {
                if (colonist.Downed) return "倒下";
                if (colonist.health.HasHediffsNeedingTend()) return "受伤";
                if (colonist.health.summaryHealth.SummaryHealthPercent < 0.8f) return "健康一般";
                return "健康";
            }
            catch
            {
                return "状态未知";
            }
        }

        private string GetMoodStatus(Pawn colonist)
        {
            try
            {
                var mood = colonist.needs?.mood;
                if (mood == null) return "未知";
                
                return mood.CurLevel switch
                {
                    < 0.2f => "极度沮丧",
                    < 0.4f => "沮丧",
                    < 0.6f => "一般",
                    < 0.8f => "良好",
                    _ => "极佳"
                };
            }
            catch
            {
                return "心情未知";
            }
        }

        private string GetCurrentTask(Pawn colonist)
        {
            try
            {
                var job = colonist.CurJob;
                return job?.def.reportString ?? "空闲";
            }
            catch
            {
                return "任务未知";
            }
        }

        private List<ThreatInfo> AnalyzeResourceThreats(Map map)
        {
            var threats = new List<ThreatInfo>();
            
            // 食物短缺检查
            var foodCount = GetFoodCount(map);
            var colonistCount = map.mapPawns.FreeColonistsCount;
            
            if (foodCount < colonistCount * 5)
            {
                threats.Add(new ThreatInfo
                {
                    Type = "资源短缺",
                    Level = ThreatLevel.High,
                    Description = "食物严重短缺",
                    DetectedAt = DateTime.Now,
                    IsActive = true,
                    Details = new Dictionary<string, object>
                    {
                        ["ResourceType"] = "Food",
                        ["CurrentAmount"] = foodCount,
                        ["RequiredAmount"] = colonistCount * 10
                    }
                });
            }

            return threats;
        }

        private List<ThreatInfo> AnalyzeHealthThreats(Map map)
        {
            var threats = new List<ThreatInfo>();
            
            var colonists = map.mapPawns.FreeColonists;
            var downedCount = colonists.Count(c => c.Downed);
            
            if (downedCount > 0)
            {
                threats.Add(new ThreatInfo
                {
                    Type = "人员伤亡",
                    Level = downedCount > colonists.Count() / 2 ? ThreatLevel.Critical : ThreatLevel.Medium,
                    Description = $"{downedCount} 名殖民者倒下",
                    DetectedAt = DateTime.Now,
                    IsActive = true
                });
            }

            return threats;
        }

        private ResourcePriority DetermineResourcePriority(ThingDef resourceDef, int count)
        {
            // 简单的优先级判断逻辑
            if (resourceDef == ThingDefOf.Silver && count < 200) return ResourcePriority.Critical;
            if (resourceDef == ThingDefOf.Steel && count < 100) return ResourcePriority.High;
            if (count < 50) return ResourcePriority.High;
            if (count < 20) return ResourcePriority.Critical;
            
            return ResourcePriority.Normal;
        }

        private string DetermineResourceStatus(ThingDef resourceDef, int count)
        {
            var priority = DetermineResourcePriority(resourceDef, count);
            return priority switch
            {
                ResourcePriority.Critical => "严重短缺",
                ResourcePriority.High => "短缺",
                _ => "充足"
            };
        }

        private void AnalyzeFoodSituation(Map map, ResourceReport report)
        {
            var foodCount = GetFoodCount(map);
            var colonistCount = map.mapPawns.FreeColonistsCount;
            
            var foodStatus = new ResourceStatus
            {
                Name = "食物",
                Current = foodCount,
                Maximum = float.MaxValue,
                DailyChange = 0,
                Priority = foodCount < colonistCount * 10 ? ResourcePriority.Critical : ResourcePriority.Normal,
                Status = foodCount < colonistCount * 5 ? "严重短缺" : 
                        foodCount < colonistCount * 10 ? "短缺" : "充足"
            };
            
            report.Resources["Food"] = foodStatus;
        }

        private string DetermineOverallResourceStatus(ResourceReport report)
        {
            if (report.CriticalShortages.Count > 0)
                return $"资源危机 - {report.CriticalShortages.Count} 项严重短缺";
            
            var shortageCount = report.Resources.Values.Count(r => r.Priority == ResourcePriority.High);
            if (shortageCount > 0)
                return $"资源紧张 - {shortageCount} 项资源短缺";
            
            return "资源状况良好";
        }

        private string GetThreatLevelDescription(ThreatLevel level)
        {
            return level switch
            {
                ThreatLevel.None => "无威胁",
                ThreatLevel.Low => "轻微威胁",
                ThreatLevel.Medium => "中等威胁",
                ThreatLevel.High => "高威胁",
                ThreatLevel.Critical => "危急威胁",
                _ => "未知威胁"
            };
        }

        #endregion
    }
}
