using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Modules.World;
using RimAI.Core.Contracts.Tooling;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Modules.Tooling.Tools
{
    /// <summary>
    /// 示例工具：获取殖民地当前综合状态摘要。
    /// </summary>
    public class GetColonyStatusTool : IRimAITool
    {
        private readonly IWorldDataService _worldDataService;

        public GetColonyStatusTool(IWorldDataService worldDataService)
        {
            _worldDataService = worldDataService;
        }

        public string Name => "get_colony_status";

        public string Description => "获取殖民地当前状态的综合摘要，包括殖民者数量、食物存量和威胁等级。";

        public ToolFunction GetSchema()
        {
            // 无参数工具，但包含描述信息
            return new ToolFunction
            {
                Name = Name,
                Description = Description,  // 添加描述信息
                Arguments = "{\"type\":\"object\",\"properties\":{}}"  // 标准的空对象 JSON Schema
            };
        }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var summary = await _worldDataService.GetColonySummaryAsync();

            // 支持 LightningFast：当编排层注入 __fastResponse=true 时，直接返回面向玩家的简短字符串
            try
            {
                if (parameters != null && parameters.TryGetValue("__fastResponse", out var v) && v is bool b && b)
                {
                    var threatZh = string.IsNullOrEmpty(summary.ThreatLevel) ? "未知" : (summary.ThreatLevel == "Low" ? "低" : (summary.ThreatLevel == "High" ? "高" : summary.ThreatLevel));
                    return $"殖民者 {summary.ColonistCount} 人，食物存量 {summary.FoodStockpile}，威胁等级：{threatZh}";
                }
            }
            catch { /* ignore */ }

            // 常规路径：返回对象，后续由 LLM 进行总结
            return summary;
        }
    }
}
