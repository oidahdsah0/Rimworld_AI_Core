using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Tools;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Tools
{
    public class GetColonyStatusTool : IRimAITool
    {
        private readonly IWorldDataService _worldDataService;
        public GetColonyStatusTool(IWorldDataService worldDataService)
        {
            _worldDataService = worldDataService;
        }

        public string Name => "get_colony_status";
        public string Description => "获取殖民地当前刻数（示例）。未来返回资源与心情摘要。";

        public ToolDefinition GetSchema()
        {
            // 无参数示例
            var func = new JObject
            {
                ["name"] = Name,
                ["description"] = Description,
                ["parameters"] = new JObject
                {
                    ["type"] = "object",
                    ["properties"] = new JObject(),
                    ["required"] = new JArray()
                }
            };
            return new ToolDefinition { Function = func };
        }

        public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var tick = await _worldDataService.GetCurrentGameTickAsync();
            return new { tick };
        }
    }
}