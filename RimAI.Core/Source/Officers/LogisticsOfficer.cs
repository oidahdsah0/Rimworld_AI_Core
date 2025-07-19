using RimAI.Framework.API;
using RimAI.Framework.LLM.Models;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core.Officers
{
    /// <summary>
    /// 物流官员 - 负责资源管理和供应链优化
    /// </summary>
    public class LogisticsOfficer
    {
        private static LogisticsOfficer _instance;
        public static LogisticsOfficer Instance => _instance ??= new LogisticsOfficer();

        /// <summary>
        /// 分析资源状况并提供建议
        /// </summary>
        public async Task<string> AnalyzeResourcesAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            try
            {
                string resourceStatus = GetResourceStatus();
                string prompt = $@"作为 RimWorld 殖民地物流专家，请分析以下资源状况并提供优化建议：
{resourceStatus}

请提供：
1. 资源短缺预警
2. 生产优先级建议
3. 储存优化方案
4. 贸易机会分析

要求简洁实用，重点突出关键问题。";

                // 使用事实性选项确保建议的准确性
                var options = RimAIAPI.Options.Factual();
                string response = await RimAIAPI.SendMessageAsync(prompt, options);

                Log.Message("[LogisticsOfficer] 资源分析完成");
                return response ?? "无法获取资源分析";
            }
            catch (Exception ex)
            {
                Log.Error($"[LogisticsOfficer] 资源分析失败: {ex.Message}");
                return $"分析失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 获取生产建议
        /// </summary>
        public async Task<string> GetProductionAdviceAsync()
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            try
            {
                string productionStatus = GetProductionStatus();
                string prompt = $@"作为生产管理专家，请分析当前生产状况并提供优化建议：
{productionStatus}

重点关注：
1. 生产效率提升
2. 工作台配置优化
3. 人员分工建议
4. 自动化改进方案";

                var response = await RimAIAPI.SendMessageAsync(prompt);
                Log.Message("[LogisticsOfficer] 生产建议生成完成");
                return response ?? "无法获取生产建议";
            }
            catch (Exception ex)
            {
                Log.Error($"[LogisticsOfficer] 生产建议生成失败: {ex.Message}");
                return $"生成失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 实时库存监控
        /// </summary>
        public async Task<string> MonitorInventoryAsync(Action<string> onUpdate = null)
        {
            if (!RimAIAPI.IsInitialized)
            {
                return "RimAI Framework 未初始化";
            }

            try
            {
                string inventoryData = GetInventoryData();
                string prompt = $@"作为库存管理系统，请监控以下库存状态并提供实时建议：
{inventoryData}

提供：
1. 紧急补货提醒
2. 库存过剩警告
3. 季节性储备建议
4. 物品处置方案

要求简短明确，适合实时监控。";

                var result = new StringBuilder();
                
                // 使用流式 API 进行实时监控
                await RimAIAPI.SendStreamingMessageAsync(
                    prompt,
                    chunk =>
                    {
                        result.Append(chunk);
                        onUpdate?.Invoke(result.ToString());
                    }
                );

                Log.Message("[LogisticsOfficer] 库存监控完成");
                return result.ToString();
            }
            catch (Exception ex)
            {
                Log.Error($"[LogisticsOfficer] 库存监控失败: {ex.Message}");
                return $"监控失败: {ex.Message}";
            }
        }

        #region 数据收集方法

        private string GetResourceStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var items = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item).ToList();
            var foodItems = items.Where(t => t.def.IsIngestible).Count();
            var materials = items.Where(t => t.def.category == ThingCategory.Item && t.def.stuffProps != null).Count();
            var weapons = items.Where(t => t.def.IsWeapon).Count();

            return $@"资源概况:
- 食物储备: {foodItems} 单位
- 建材存量: {materials} 单位  
- 武器装备: {weapons} 件
- 总物品数: {items.Count}";
        }

        private string GetProductionStatus()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var workTables = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.hasInteractionCell && b.def.category == ThingCategory.Building)
                .Count();

            var colonists = map.mapPawns.FreeColonistsCount;

            return $@"生产状况:
- 工作台数量: {workTables}
- 可用工人: {colonists}
- 工作台利用率: 需要分析
- 生产瓶颈: 需要识别";
        }

        private string GetInventoryData()
        {
            var map = Find.CurrentMap;
            if (map == null) return "无有效地图数据";

            var storageBuildings = map.listerBuildings.allBuildingsColonist
                .Where(b => b.def.building?.isInert == false && b is Building_Storage)
                .Count();

            var totalItems = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item).Count();

            return $@"库存数据:
- 储存建筑: {storageBuildings}
- 物品总数: {totalItems}
- 储存空间使用率: 需要计算
- 关键物品状态: 需要检查";
        }

        #endregion
    }
}
