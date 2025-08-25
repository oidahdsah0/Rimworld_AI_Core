using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetResourceOverviewTool : IRimAITool
    {
        public string Name => "get_resource_overview";
        public string Description => "Get overview of counted resources (including mod items marked as CountAsResource) with rough daily use and days left estimates.";
        public string DisplayName => "资源概览";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new
        {
            type = "object",
            properties = new { },
            required = new string[] { }
        });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            var json = JsonConvert.SerializeObject(new
            {
                type = "function",
                function = new
                {
                    name = Name,
                    description = Description,
                    parameters = parameters
                }
            });
            return json;
        }
    }
}
