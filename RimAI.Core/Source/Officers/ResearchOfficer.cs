using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Officers.Base;
using RimWorld;
using Verse;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 科研官员 - 负责研究项目管理和技术发展
    /// </summary>
    public class ResearchOfficer : OfficerBase
    {
        private static ResearchOfficer _instance;
        public static ResearchOfficer Instance => _instance ??= new ResearchOfficer();

        public override string Name => "科研官员";
        public override string Description => "管理研究项目和技术发展";
        public override string IconPath => "UI/Icons/Research";
        public override OfficerRole Role => OfficerRole.Research;

        private ResearchOfficer() { }

        #region 核心分析方法

        protected override async Task<Dictionary<string, object>> BuildContextAsync(CancellationToken cancellationToken = default)
        {
            var context = await base.BuildContextAsync(cancellationToken);
            
            // 添加科研特定的上下文信息
            context["CurrentResearch"] = GetCurrentResearch();
            context["AvailableResearch"] = GetAvailableResearch();
            context["ResearchProgress"] = GetResearchProgress();
            context["ResearchFacilities"] = GetResearchFacilities();
            context["Researchers"] = GetResearchers();
            context["TechLevel"] = GetTechLevel();
            
            return context;
        }

        /// <summary>
        /// 获取研究建议
        /// </summary>
        public async Task<string> GetResearchRecommendationsAsync(CancellationToken cancellationToken = default)
        {
            if (!IsAvailable)
                return GetUnavailableMessage();

            try
            {
                var context = await BuildContextAsync(cancellationToken);
                var prompt = BuildResearchPrompt(context);
                var options = CreateLLMOptions(temperature: 0.7f);

                return await _llmService.SendMessageAsync(prompt, options, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取研究建议失败: {ex.Message}");
                return GetErrorMessage(ex.Message);
            }
        }

        /// <summary>
        /// 分析研究优先级
        /// </summary>
        public async Task<List<ResearchPriority>> AnalyzeResearchPrioritiesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await BuildContextAsync(cancellationToken);
                var prompt = BuildPriorityAnalysisPrompt(context);
                var options = CreateLLMOptions(temperature: 0.5f, forceJson: true);

                var result = await _llmService.SendJsonRequestAsync<ResearchAnalysisResult>(prompt, options, cancellationToken);
                return result?.Priorities ?? new List<ResearchPriority>();
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 研究优先级分析失败: {ex.Message}");
                return new List<ResearchPriority>();
            }
        }

        #endregion

        #region 数据收集方法

        private object GetCurrentResearch()
        {
            try
            {
                var currentProject = Find.ResearchManager.currentProj;
                if (currentProject == null)
                    return "无正在进行的研究";

                return new
                {
                    Name = currentProject.label,
                    Description = currentProject.description,
                    Progress = Find.ResearchManager.GetProgress(currentProject),
                    TotalCost = currentProject.CostApparent,
                    RequiredResearchBench = currentProject.requiredResearchBuilding?.label ?? "基础研究台",
                    Prerequisites = currentProject.prerequisites?.Select(p => p.label)?.ToList() ?? new List<string>()
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取当前研究信息失败: {ex.Message}");
                return "研究信息获取失败";
            }
        }

        private object GetAvailableResearch()
        {
            try
            {
                var availableProjects = DefDatabase<ResearchProjectDef>.AllDefs
                    .Where(p => p.CanStartNow)
                    .Take(10)
                    .Select(p => new
                    {
                        Name = p.label,
                        Cost = p.CostApparent,
                        TechLevel = p.techLevel.ToString(),
                        RequiredBench = p.requiredResearchBuilding?.label ?? "基础研究台",
                        Category = GetResearchCategory(p)
                    })
                    .ToList();

                return availableProjects;
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取可用研究失败: {ex.Message}");
                return new List<object>();
            }
        }

        private object GetResearchProgress()
        {
            try
            {
                var completedCount = DefDatabase<ResearchProjectDef>.AllDefs.Count(p => p.IsFinished);
                var totalCount = DefDatabase<ResearchProjectDef>.AllDefs.Count();
                var progressPercentage = totalCount > 0 ? (completedCount * 100 / totalCount) : 0;

                return new
                {
                    Completed = completedCount,
                    Total = totalCount,
                    ProgressPercentage = progressPercentage,
                    CurrentPoints = Find.ResearchManager.ResearchPointsPerWorkTick * GenTicks.TicksPerRealSecond
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取研究进度失败: {ex.Message}");
                return "研究进度获取失败";
            }
        }

        private object GetResearchFacilities()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return new List<object>();

                var facilities = map.listerBuildings.allBuildingsColonist
                    .Where(b => b.def.building?.isResearchBench == true)
                    .Select(b => new
                    {
                        Name = b.def.label,
                        Position = b.Position.ToString(),
                        PowerConnected = b.TryGetComp<CompPowerTrader>()?.PowerOn ?? true,
                        ResearchSpeed = GetResearchSpeed(b)
                    })
                    .ToList();

                return facilities;
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取研究设施信息失败: {ex.Message}");
                return new List<object>();
            }
        }

        private object GetResearchers()
        {
            try
            {
                var map = Find.CurrentMap;
                if (map == null) return new List<object>();

                var researchers = map.mapPawns.FreeColonists
                    .Where(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level > 0)
                    .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Intellectual).Level)
                    .Take(5)
                    .Select(p => new
                    {
                        Name = p.Name.ToStringShort,
                        IntellectualLevel = p.skills.GetSkill(SkillDefOf.Intellectual).Level,
                        Passion = p.skills.GetSkill(SkillDefOf.Intellectual).passion.ToString(),
                        CurrentTask = p.CurJob?.def.reportString ?? "空闲",
                        Available = !p.Downed && !p.InBed()
                    })
                    .ToList();

                return researchers;
            }
            catch (Exception ex)
            {
                Log.Error($"[{Name}] 获取研究人员信息失败: {ex.Message}");
                return new List<object>();
            }
        }

        private string GetTechLevel()
        {
            try
            {
                return Faction.OfPlayer.def.techLevel.ToString();
            }
            catch
            {
                return "未知";
            }
        }

        #endregion

        #region 辅助方法

        private string GetResearchCategory(ResearchProjectDef project)
        {
            // 根据研究项目的特点分类
            if (project.tags?.Contains(ResearchProjectTagDefOf.ShipRelated) == true)
                return "飞船技术";
            if (project.requiredResearchBuilding?.defName.Contains("Hi") == true)
                return "高级研究";
            if (project.techLevel <= TechLevel.Medieval)
                return "基础研究";
            if (project.techLevel >= TechLevel.Spacer)
                return "太空技术";
            
            return "通用研究";
        }

        private float GetResearchSpeed(Building facility)
        {
            try
            {
                var comp = facility.TryGetComp<CompAffectedByFacilities>();
                if (comp != null)
                {
                    return comp.LinkedFacilitiesListForReading.Sum(f => f.StatValue) + 1.0f;
                }
                return 1.0f;
            }
            catch
            {
                return 1.0f;
            }
        }

        private string BuildResearchPrompt(Dictionary<string, object> context)
        {
            return $@"作为 RimWorld 殖民地科研顾问，请基于以下信息提供研究建议：

【当前研究状况】
当前项目：{context["CurrentResearch"]}
研究进度：{context["ResearchProgress"]}
技术等级：{context["TechLevel"]}

【可用资源】
研究设施：{context["ResearchFacilities"]}
研究人员：{context["Researchers"]}
可选项目：{context["AvailableResearch"]}

【殖民地状况】
{context["ColonyOverview"]}

请提供：
1. 下一步研究项目推荐（考虑当前需求和威胁）
2. 研究效率优化建议
3. 研究人员安排建议
4. 研究设施改进建议

要求：简洁实用，重点突出优先级和实际效益。";
        }

        private string BuildPriorityAnalysisPrompt(Dictionary<string, object> context)
        {
            return $@"分析以下研究项目的优先级，并返回JSON格式的结果：

【可用研究】
{context["AvailableResearch"]}

【殖民地状况】
{context["ColonyOverview"]}

请返回JSON格式：
{{
    ""priorities"": [
        {{
            ""projectName"": ""项目名称"",
            ""priority"": ""High/Medium/Low"",
            ""reason"": ""选择理由"",
            ""expectedBenefit"": ""预期收益""
        }}
    ]
}}";
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 研究优先级
    /// </summary>
    public class ResearchPriority
    {
        public string ProjectName { get; set; }
        public string Priority { get; set; }
        public string Reason { get; set; }
        public string ExpectedBenefit { get; set; }
    }

    /// <summary>
    /// 研究分析结果
    /// </summary>
    public class ResearchAnalysisResult
    {
        public List<ResearchPriority> Priorities { get; set; } = new List<ResearchPriority>();
    }

    #endregion
}
