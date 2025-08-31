using System.Collections.Generic;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetWildlifeOpportunitiesTool : IRimAITool
    {
        public string Name => "get_wildlife_opportunities";
    public string DisplayName => "tool.display.get_wildlife_opportunities";
        public int Level => 2;
        public string Description => "按物种聚合当前地图的野生动物，给出数量、风险（复仇/捕食/爆炸）与收益（肉/皮革）评估。";
    public string ParametersJson => "{ }"; // v1 无参数

        public string BuildToolJson()
        {
            var dict = new Dictionary<string, object>
            {
                ["Name"] = Name,
                ["DisplayName"] = DisplayName,
                ["Level"] = Level,
                ["Description"] = Description,
                ["ParametersSchema"] = new { }
            };
            return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
        }
    }
}
