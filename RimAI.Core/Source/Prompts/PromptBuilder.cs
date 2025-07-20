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
        private static PromptBuilder _instance;
        public static PromptBuilder Instance => _instance ??= new PromptBuilder();

        private readonly Dictionary<string, PromptTemplate> _templates;
        private readonly Regex _variablePattern;

        private PromptBuilder()
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
        /// 初始化默认提示词模板
        /// </summary>
        private void InitializeDefaultTemplates()
        {
            // 总督快速决策模板
            RegisterTemplate("governor.quick_decision", new PromptTemplate
            {
                Name = "总督快速决策",
                Description = "用于紧急情况的快速决策建议",
                Template = @"作为RimWorld殖民地管理AI总督，请对以下紧急情况提供简明扼要的应对建议（不超过{{maxWords}}字）：

【紧急情况】
{{situation}}

【当前殖民地状态】
- 殖民者数量：{{colonistCount}}
- 资源状况：{{resourceStatus}}
- 威胁等级：{{threatLevel}}
- 当前季节：{{season}}

【要求】
请提供具体可行的应对措施，优先考虑殖民者安全。

【重要限制】
- 仅提供游戏内管理建议
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致",
                Variables = new Dictionary<string, string>
                {
                    ["maxWords"] = "100",
                    ["colonistCount"] = "未知",
                    ["resourceStatus"] = "未知",
                    ["threatLevel"] = "未知",
                    ["season"] = "未知"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 200,
                    Temperature = 0.7f,
                    ResponseFormat = "text",
                    SafetyRules = new List<string>
                    {
                        "仅提供游戏内管理建议",
                        "不得生成NSFW内容",
                        "保持专业游戏管理语调"
                    }
                }
            });

            // 总督详细策略模板
            RegisterTemplate("governor.detailed_strategy", new PromptTemplate
            {
                Name = "总督详细策略",
                Description = "用于深度分析的详细管理策略",
                Template = @"作为RimWorld殖民地管理专家，请根据以下殖民地状态制定详细的管理策略和优先事项：

【殖民地状态分析】
{{colonyStatus}}

【详细信息】
- 殖民者详情：{{colonistDetails}}
- 资源库存：{{resourceInventory}}
- 建筑设施：{{buildings}}
- 当前研究：{{research}}
- 威胁情况：{{threats}}

【请提供以下分析】
1. 当前状况评估
2. 优先处理事项（按紧急程度排序）
3. 中期发展规划（1-2个游戏年）
4. 长期发展目标
5. 潜在风险预警
6. 资源分配建议

【重要限制】
- 仅提供游戏内策略建议
- 不得生成NSFW、暴力、政治敏感等不当内容
- 不得讨论现实世界敏感话题
- 保持专业、建设性的游戏管理语调
- 返回语言要与用户所写内容一致",
                Variables = new Dictionary<string, string>
                {
                    ["colonistDetails"] = "待分析",
                    ["resourceInventory"] = "待统计",
                    ["buildings"] = "待清点",
                    ["research"] = "待查询",
                    ["threats"] = "待评估"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 800,
                    Temperature = 0.6f,
                    ResponseFormat = "text"
                }
            });

            // 注意：已移除军事官员和后勤官员相关模板（这些组件已被删除）
            // 保留基础事件解说功能

            // 事件解说模板
            RegisterTemplate("narrator.event_narration", new PromptTemplate
            {
                Name = "事件解说",
                Description = "用于生动描述游戏事件",
                Template = @"作为专业的RimWorld事件解说员，请生动有趣地描述以下事件：

【事件描述】
{{eventDescription}}

【背景信息】
- 发生地点：{{location}}
- 涉及人员：{{involvedPersonnel}}
- 当前情况：{{currentSituation}}
- 影响范围：{{impact}}

【解说要求】
- 使用生动有趣的语言
- 创造紧张或幽默的氛围（适合事件性质）
- 突出关键细节和转折点
- 保持解说的连贯性

【重要限制】
- 仅描述游戏内事件和情况
- 不得生成NSFW、过度暴力内容
- 不得讨论现实世界敏感话题
- 保持生动有趣但适宜的游戏解说风格
- 返回语言要与用户所写内容一致",
                Variables = new Dictionary<string, string>
                {
                    ["location"] = "殖民地",
                    ["involvedPersonnel"] = "殖民者",
                    ["currentSituation"] = "进行中",
                    ["impact"] = "待评估"
                },
                Constraints = new PromptConstraints
                {
                    MaxTokens = 300,
                    Temperature = 0.8f,
                    ResponseFormat = "text",
                    RequireStreaming = true
                }
            });

            Log.Message($"[PromptBuilder] Initialized {_templates.Count} default templates");
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
