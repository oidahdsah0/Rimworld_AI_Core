using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using RimAI.Framework.LLM.Services;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 增强的总督官员 - 展示新 API 的各种使用方式
    /// </summary>
    public class Governor
    {
        private static Governor _instance;
        public static Governor Instance => _instance ??= new Governor();

        /// <summary>
        /// 使用 JSON 服务分析殖民地状态
        /// </summary>
        public async Task<ColonyAnalysis> AnalyzeColonyAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                Log.Error("RimAI Framework 未初始化");
                return null;
            }

            var jsonService = RimAIAPI.GetJsonService();
            if (jsonService == null)
            {
                Log.Error("JSON 服务不可用");
                return null;
            }

            try
            {
                string prompt = BuildAnalysisPrompt();
                var options = RimAIAPI.Options.Json(temperature: 0.5);
                
                // 使用 JSON 服务确保结构化响应
                var response = await jsonService.SendJsonRequestAsync<ColonyAnalysis>(prompt, options);

                if (response.Success)
                {
                    Log.Message("殖民地分析完成");
                    return response.Data;
                }
                else
                {
                    Log.Error($"分析失败: {response.Error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"殖民地分析异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使用自定义服务进行高级分析
        /// </summary>
        public async Task<string> GetAdvancedRecommendationAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                Log.Error("RimAI Framework 未初始化");
                return "Framework 未初始化";
            }

            var customService = RimAIAPI.GetCustomService();
            if (customService == null)
            {
                Log.Error("自定义服务不可用");
                return "自定义服务不可用";
            }

            try
            {
                string prompt = BuildAdvancedPrompt();
                
                // 使用创意选项获得更丰富的建议
                var options = RimAIAPI.Options.Creative(temperature: 1.1);
                
                var response = await RimAIAPI.SendMessageAsync(prompt, options);
                
                if (!string.IsNullOrEmpty(response))
                {
                    Log.Message("高级建议生成完成");
                    return response;
                }
                else
                {
                    return "无法生成高级建议";
                }
            }
            catch (Exception ex)
            {
                Log.Error($"高级建议生成失败: {ex.Message}");
                return $"生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 使用流式 API 进行实时建议
        /// </summary>
        public async Task<string> GetRealTimeAdviceAsync(Action<string> onPartialUpdate = null)
        {
            if (!RimAIAPI.IsInitialized)
            {
                Log.Error("RimAI Framework 未初始化");
                return "Framework 未初始化";
            }

            try
            {
                string prompt = BuildRealTimePrompt();
                var fullResponse = new StringBuilder();
                
                // 强制使用流式模式，即使全局设置是非流式
                var options = RimAIAPI.Options.Streaming(temperature: 0.8, maxTokens: 1500);
                
                await RimAIAPI.SendStreamingMessageAsync(
                    prompt,
                    chunk =>
                    {
                        fullResponse.Append(chunk);
                        onPartialUpdate?.Invoke(fullResponse.ToString());
                    },
                    options
                );

                string result = fullResponse.ToString();
                Log.Message("实时建议生成完成");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"实时建议生成失败: {ex.Message}");
                return $"生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 使用事实性选项进行精确分析
        /// </summary>
        public async Task<string> GetFactualAnalysisAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "Framework 未初始化";
            }

            try
            {
                string prompt = BuildFactualPrompt();
                
                // 使用事实性选项确保准确性
                var options = RimAIAPI.Options.Factual(temperature: 0.2);
                
                string response = await RimAIAPI.SendMessageAsync(prompt, options);
                
                if (!string.IsNullOrEmpty(response))
                {
                    Log.Message("事实性分析完成");
                    return response;
                }
                else
                {
                    return "无法生成分析";
                }
            }
            catch (Exception ex)
            {
                Log.Error($"事实性分析失败: {ex.Message}");
                return $"分析失败: {ex.Message}";
            }
        }

        #region 辅助方法

        private string BuildAnalysisPrompt()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            // 安全地获取动物数量
            int animalCount = 0;
            try
            {
                animalCount = map.mapPawns.AllPawnsSpawned.Where(p => p.RaceProps.Animal && p.Faction == Faction.OfPlayer).Count();
            }
            catch
            {
                animalCount = 0; // 如果获取失败，使用默认值
            }

            return $@"请分析以下 RimWorld 殖民地状态并提供 JSON 格式的分析结果：
殖民者数量: {map.mapPawns.FreeColonistsCount}
囚犯数量: {map.mapPawns.PrisonersOfColonyCount}
动物数量: {animalCount}

请返回包含以下字段的 JSON:
- totalColonists: 殖民者总数
- averageMood: 平均心情 (0-100)
- criticalIssues: 关键问题列表
- recommendedAction: 推荐行动";
        }

        private string BuildAdvancedPrompt()
        {
            return @"作为 RimWorld 殖民地高级管理顾问，请提供创新性的管理策略和发展建议。
考虑以下因素：
1. 长期发展规划
2. 风险管理策略
3. 创新建设方案
4. 人员配置优化

请提供详细且富有创意的建议。";
        }

        private string BuildRealTimePrompt()
        {
            return @"作为 RimWorld 殖民地实时助手，请提供当前最需要关注的事项和即时建议。
要求简洁明了，重点突出，适合实时显示。";
        }

        private string BuildFactualPrompt()
        {
            return @"请基于当前殖民地数据，提供准确的统计分析和基于事实的管理建议。
要求：
1. 数据准确
2. 逻辑清晰
3. 建议可执行
4. 避免主观判断";
        }

        #endregion
    }

    /// <summary>
    /// 殖民地分析结果结构
    /// </summary>
    public class ColonyAnalysis
    {
        public int TotalColonists { get; set; }
        public float AverageMood { get; set; }
        public string[] CriticalIssues { get; set; }
        public string RecommendedAction { get; set; }
    }
}
