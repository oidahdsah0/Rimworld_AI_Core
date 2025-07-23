using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Prompts
{
    /// <summary>
    /// 提示词构建器实现 - 负责管理和构建提示词模板
    /// </summary>
    public class PromptBuilder : IPromptBuilder
    {
        private readonly Dictionary<string, PromptTemplate> _templates;
        private readonly Regex _variablePattern;

        public PromptBuilder()
        {
            _templates = new Dictionary<string, PromptTemplate>();
            _variablePattern = new Regex(@"\{\{(\w+)\}\}", RegexOptions.Compiled);
            
            InitializeDefaultTemplates();
        }

        public string BuildPrompt(string templateId, Dictionary<string, object> context)
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                Log.Warning($"[PromptBuilder] Template '{templateId}' not found, using fallback");
                return BuildFallbackPrompt(templateId, context);
            }

            try
            {
                var result = template.Template;

                // 替换模板变量
                result = _variablePattern.Replace(result, match =>
                {
                    var variableName = match.Groups[1].Value;
                    
                    if (context.TryGetValue(variableName, out var value))
                    {
                        return value?.ToString() ?? string.Empty;
                    }
                    
                    // 检查模板默认变量
                    if (template.Variables.TryGetValue(variableName, out var defaultValue))
                    {
                        return defaultValue;
                    }
                    
                    Log.Warning($"[PromptBuilder] Variable '{variableName}' not found in context for template '{templateId}'");
                    return $"{{{{{variableName}}}}}"; // 保持原样
                });

                Log.Message($"[PromptBuilder] Built prompt for template '{templateId}'");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"[PromptBuilder] Failed to build prompt for template '{templateId}': {ex.Message}");
                return BuildFallbackPrompt(templateId, context);
            }
        }

        public void RegisterTemplate(string id, PromptTemplate template)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("Template ID cannot be null or empty", nameof(id));
            
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            template.Id = id;
            template.LastModified = DateTime.Now;
            
            _templates[id] = template;
            Log.Message($"[PromptBuilder] Registered template '{id}': {template.Name}");
        }

        public PromptTemplate GetTemplate(string id)
        {
            _templates.TryGetValue(id, out var template);
            return template;
        }

        public bool TemplateExists(string id)
        {
            return _templates.ContainsKey(id);
        }

        /// <summary>
        /// 初始化默认提示词模板 - 仅保留必要的核心模板
        /// </summary>
        private void InitializeDefaultTemplates()
        {
            // 🎯 总督用户查询模板 - 用于响应用户的问候和问题
            RegisterTemplate("governor.user_query", new PromptTemplate
            {
                Name = "总督用户查询响应",
                Description = "总督响应用户具体问题的模板",
                Template = @"你是RimWorld殖民地的AI总督助手。用户向你问好或询问问题，请以友善、专业的方式回应。

用户说：{{userQuery}}

当前殖民地状况：
- 殖民者：{{colonistCount}}人 ({{colonistStatus}})
- 食物储备：{{foodDaysRemaining}}天
- 威胁等级：{{threatLevel}}
- 总体风险：{{overallRiskLevel}}
- 状态分析：{{quickAnalysisSummary}}

请根据用户的问题类型回应：
- 如果是简单问候（如你好、hi等），友善回应并简要介绍殖民地现状
- 如果是具体问题，基于殖民地数据提供专业建议
- 如果是紧急情况，优先给出应对措施

保持友善、专业的总督风格，回应要简洁实用。",
                Variables = new Dictionary<string, string>
                {
                    ["colonistCount"] = "未知",
                    ["colonistStatus"] = "状态未知", 
                    ["foodDaysRemaining"] = "未知",
                    ["threatLevel"] = "未知",
                    ["overallRiskLevel"] = "未知",
                    ["quickAnalysisSummary"] = "分析不可用"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 400,
                    Temperature = 0.8f,
                    ResponseFormat = "text"
                }
            });

            // 🎯 总督快速状态模板 - 用于快速状态查询和概览
            RegisterTemplate("governor.quick_status", new PromptTemplate
            {
                Name = "总督快速状态",
                Description = "总督提供殖民地快速状态概览",
                Template = @"作为殖民地总督，请提供当前殖民地的状态概览和管理建议。

【殖民地现状】
- 人口：{{colonistCount}}人 ({{colonistStatus}})
- 食物：{{foodDaysRemaining}}天储备
- 威胁：{{threatLevel}}级别
- 风险：{{overallRiskLevel}}
- 季节：{{season}}，天气：{{weather}}

【分析摘要】
{{quickAnalysisSummary}}

请提供：
1. 当前状况简评
2. 需要关注的优先事项
3. 简要管理建议

保持简洁专业的总督风格。",
                Variables = new Dictionary<string, string>
                {
                    ["colonistCount"] = "未知",
                    ["colonistStatus"] = "状态未知",
                    ["foodDaysRemaining"] = "未知", 
                    ["threatLevel"] = "未知",
                    ["overallRiskLevel"] = "未知",
                    ["season"] = "未知",
                    ["weather"] = "未知",
                    ["quickAnalysisSummary"] = "分析不可用"
                }
            });

            // 🎯 总督详细分析模板 - 用于深度分析和详细建议
            RegisterTemplate("governor.detailed_analysis", new PromptTemplate
            {
                Name = "总督详细分析",
                Description = "总督提供深度分析和详细管理策略",
                Template = @"作为RimWorld殖民地总督，请基于当前殖民地状况提供全面的管理分析和策略建议。

【殖民地详细状况】
- 殖民者详情：{{colonistDetails}}
- 资源库存：{{resourceInventory}}
- 建筑设施：{{buildings}}
- 当前研究：{{research}}
- 威胁情况：{{threats}}
- 季节和天气：{{season}} - {{weather}}

【深度分析要求】
1. 当前状况全面评估
2. 优先处理事项（按紧急程度排序）
3. 短期应对策略（1-2个季度）
4. 中期发展规划（1-2个游戏年）
5. 潜在风险预警和预防措施
6. 资源分配优化建议

请提供具体可行的管理建议，优先考虑殖民者安全和殖民地可持续发展。",
                Variables = new Dictionary<string, string>
                {
                    ["colonistDetails"] = "待分析",
                    ["resourceInventory"] = "待统计",
                    ["buildings"] = "待清点",
                    ["research"] = "待查询",
                    ["threats"] = "待评估",
                    ["season"] = "未知",
                    ["weather"] = "未知"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 800,
                    Temperature = 0.6f,
                    ResponseFormat = "text"
                }
            });

            // 🎯 总督实时更新模板 - 用于流式响应和实时建议
            RegisterTemplate("governor.live_updates", new PromptTemplate
            {
                Name = "总督实时更新",
                Description = "总督提供实时状态更新和动态建议",
                Template = @"作为殖民地总督，我将持续监控并提供实时管理建议。

【当前关注焦点】
{{situation}}

【实时状态】
- 殖民者：{{colonistCount}}人 ({{colonistStatus}})
- 紧急事项：{{urgentMatters}}
- 威胁级别：{{threatLevel}}
- 资源警报：{{resourceAlerts}}

【实时建议】
基于当前情况，我建议：

1. 立即行动：{{immediateActions}}
2. 密切关注：{{watchPoints}}
3. 准备应对：{{preparations}}

我将继续监控情况变化并及时更新建议。有任何紧急情况请随时询问。",
                Variables = new Dictionary<string, string>
                {
                    ["situation"] = "正常运营",
                    ["colonistCount"] = "未知",
                    ["colonistStatus"] = "状态未知",
                    ["urgentMatters"] = "暂无",
                    ["threatLevel"] = "低",
                    ["resourceAlerts"] = "暂无",
                    ["immediateActions"] = "保持当前状态",
                    ["watchPoints"] = "常规监控",
                    ["preparations"] = "基础准备"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 500,
                    Temperature = 0.7f,
                    ResponseFormat = "text",
                    RequireStreaming = true
                }
            });

            Log.Message($"[PromptBuilder] Initialized {_templates.Count} essential templates");
        }

        /// <summary>
        /// 构建后备提示词（当模板不存在时使用）
        /// </summary>
        private string BuildFallbackPrompt(string templateId, Dictionary<string, object> context)
        {
            var situationText = context.ContainsKey("situation") ? context["situation"]?.ToString() : "未指定情况";
            
            return $@"作为RimWorld殖民地AI助手，请对以下情况提供建议：

{situationText}

重要限制：
- 仅提供游戏内建议
- 不得生成不当内容
- 保持专业游戏管理语调
- 返回语言与输入一致

（注：使用后备模板，模板ID: {templateId})";
        }

        /// <summary>
        /// 获取所有可用模板ID
        /// </summary>
        public List<string> GetAvailableTemplateIds()
        {
            return new List<string>(_templates.Keys);
        }

        /// <summary>
        /// 获取模板统计信息
        /// </summary>
        public string GetStats()
        {
            return $"提示词模板统计: {_templates.Count} 个模板已加载";
        }
    }
}
