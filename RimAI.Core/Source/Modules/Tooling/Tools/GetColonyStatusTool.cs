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
            // 无参数工具
            return new ToolFunction
            {
                Name = Name,
                Arguments = "{}" // 空对象
            };
        }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var summary = await _worldDataService.GetColonySummaryAsync();
            // 返回可序列化对象
            return summary;
        }
    }
}
