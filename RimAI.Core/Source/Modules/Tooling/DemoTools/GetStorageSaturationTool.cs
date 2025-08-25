using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetStorageSaturationTool : IRimAITool
    {
        public string Name => "get_storage_saturation";
        public string Description => "Get per-storage usage saturation across stockpiles and storage buildings.";
        public string DisplayName => "仓储饱和度";
        public int Level => 1;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
