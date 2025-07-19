using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
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
    /// 军事官员 - 负责防务分析和战术建议
    /// </summary>
    public class MilitaryOfficer
    {
        private static MilitaryOfficer _instance;
        public static MilitaryOfficer Instance => _instance ??= new MilitaryOfficer();

        private CancellationTokenSource _currentOperation;

        /// <summary>
        /// 分析威胁级别并提供防务建议
        /// </summary>
        public async Task<string> AssessThreatLevelAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            try
            {
                string threatAnalysis = GetThreatAnalysis();
                string prompt = $@"作为 RimWorld 军事战略专家，请分析当前威胁状况并提供防务建议：
{threatAnalysis}

请提供：
1. 威胁等级评估 (低/中/高/极高)
2. 防务弱点分析
3. 紧急应对措施
4. 长期防务规划

要求简明扼要，重点突出即时威胁。";

                // 使用事实性选项确保威胁评估的准确性
                var options = RimAIAPI.Options.Factual(temperature: 0.1);
                string response = await RimAIAPI.SendMessageAsync(prompt, options);

                Log.Message("[MilitaryOfficer] 威胁评估完成");
                return response ?? "无法获取威胁评估";
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] 威胁评估失败: {ex.Message}");
                return $"评估失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 生成战术建议（使用流式 API 进行实时战术更新）
        /// </summary>
        public async Task<string> GenerateTacticalAdviceAsync(Action<string> onPartialUpdate = null, CancellationToken cancellationToken = default)
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            // 取消之前的操作
            _currentOperation?.Cancel();
            _currentOperation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                string combatSituation = GetCombatSituation();
                string prompt = $@"作为战术指挥官，请基于以下情况制定实时战术方案：
{combatSituation}

提供实时战术指导：
1. 立即行动指令
2. 人员部署建议
3. 武器配置推荐
4. 撤退路线规划

要求：指令清晰，便于快速执行。";

                var result = new StringBuilder();
                
                // 使用流式选项进行实时战术更新
                var options = RimAIAPI.Options.Streaming(temperature: 0.6, maxTokens: 1200);
                
                await RimAIAPI.SendStreamingMessageAsync(
                    prompt,
                    chunk =>
                    {
                        if (_currentOperation.Token.IsCancellationRequested) return;
                        
                        result.Append(chunk);
                        onPartialUpdate?.Invoke(result.ToString());
                    },
                    options,
                    _currentOperation.Token
                );

                if (!_currentOperation.Token.IsCancellationRequested)
                {
                    Log.Message("[MilitaryOfficer] 战术建议生成完成");
                    return result.ToString();
                }
                else
                {
                    return "战术分析已取消";
                }
            }
            catch (OperationCanceledException)
            {
                Log.Message("[MilitaryOfficer] 战术分析被用户取消");
                return "战术分析已取消";
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] 战术建议生成失败: {ex.Message}");
                return $"生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 使用 JSON 服务进行结构化战斗分析
        /// </summary>
        public async Task<CombatAnalysis> AnalyzeCombatCapabilityAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                return null;
            }

            var jsonService = RimAIAPI.GetJsonService();
            if (jsonService == null)
            {
                Log.Error("[MilitaryOfficer] JSON 服务不可用");
                return null;
            }

            try
            {
                string combatData = GetCombatData();
                string prompt = $@"请分析以下 RimWorld 殖民地战斗能力并返回 JSON 格式结果：
{combatData}

返回包含以下字段的 JSON:
- combatRating: 整体战斗力评级 (1-100)
- strengths: 战斗优势列表
- weaknesses: 战斗劣势列表  
- recommendedImprovements: 改进建议列表
- readinessLevel: 战备等级 (低/中/高)";

                var options = RimAIAPI.Options.Json(temperature: 0.4);
                var response = await jsonService.SendJsonRequestAsync<CombatAnalysis>(prompt, options);

                if (response.Success)
                {
                    Log.Message("[MilitaryOfficer] 战斗能力分析完成");
                    return response.Data;
                }
                else
                {
                    Log.Error($"[MilitaryOfficer] 分析失败: {response.Error}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] 战斗能力分析异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 紧急战斗响应（高优先级，强制流式）
        /// </summary>
        public async Task<string> EmergencyCombatResponseAsync(string emergencyDescription)
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            try
            {
                string prompt = $@"紧急战斗情况！立即提供应对指令：
{emergencyDescription}

要求：
1. 立即行动指令（3条以内）
2. 优先级排序
3. 关键注意事项
4. 失败后备方案

格式：简短明确，便于紧急执行！";

                // 强制使用流式模式以获得最快响应
                var options = RimAIAPI.Options.Streaming(temperature: 0.3, maxTokens: 500);
                string response = await RimAIAPI.SendMessageAsync(prompt, options);

                Log.Warning($"[MilitaryOfficer] 紧急战斗响应: {emergencyDescription}");
                return response ?? "无法获取紧急响应";
            }
            catch (Exception ex)
            {
                Log.Error($"[MilitaryOfficer] 紧急响应失败: {ex.Message}");
                return $"紧急响应失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 取消当前军事操作
        /// </summary>
        public void CancelCurrentOperation()
        {
            _currentOperation?.Cancel();
            Log.Message("[MilitaryOfficer] 当前军事操作已取消");
        }

        #region 数据收集方法

        private string GetThreatAnalysis()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var colonists = map.mapPawns.FreeColonistsCount;
            var weapons = map.listerThings.AllThings.Where(t => t.def.IsWeapon).Count();
            var defenses = map.listerBuildings.allBuildingsColonist
                .Where(b => (b.def.building?.turretGunDef != null) || b.def.defName.Contains("Wall"))
                .Count();

            return $@"当前防务状况:
- 可战斗殖民者: {colonists}
- 武器储备: {weapons}
- 防御建筑: {defenses}
- 地形优势: 需要评估
- 近期威胁: 未知";
        }

        private string GetCombatSituation()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var hostiles = map.mapPawns.AllPawnsSpawned.Where(p => p.HostileTo(Faction.OfPlayer)).Count();
            var colonistsCapableOfViolence = map.mapPawns.FreeColonistsSpawned.Where(p => !p.WorkTagIsDisabled(WorkTags.Violent)).Count();

            return $@"战斗态势:
- 敌对单位: {hostiles}
- 可战斗殖民者: {colonistsCapableOfViolence}
- 当前威胁等级: {(hostiles > colonistsCapableOfViolence ? "高" : hostiles > 0 ? "中" : "低")}
- 战斗状态: {(hostiles > 0 ? "交战中" : "和平状态")}";
        }

        private string GetCombatData()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var fighters = map.mapPawns.FreeColonistsSpawned.Where(p => !p.WorkTagIsDisabled(WorkTags.Violent)).Count();
            var weapons = map.listerThings.AllThings.Where(t => t.def.IsWeapon).Count();
            var armor = map.listerThings.AllThings.Where(t => t.def.IsApparel && t.def.apparel?.bodyPartGroups?.Any() == true).Count();

            return $@"战斗数据:
- 战斗人员: {fighters}
- 武器数量: {weapons}
- 护甲装备: {armor}
- 医疗设施: 需要统计
- 战略资源: 需要评估";
        }

        #endregion
    }

    /// <summary>
    /// 战斗分析结果结构
    /// </summary>
    public class CombatAnalysis
    {
        public int CombatRating { get; set; }
        public string[] Strengths { get; set; }
        public string[] Weaknesses { get; set; }
        public string[] RecommendedImprovements { get; set; }
        public string ReadinessLevel { get; set; }
    }
}
