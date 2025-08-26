using Newtonsoft.Json;

namespace RimAI.Core.Source.Modules.Tooling.DemoTools
{
    internal sealed class GetUnknownCivContactTool : IRimAITool
    {
        public string Name => "get_unknown_civ_contact";
        public string Description => "Attempt to contact an unknown civilization via gravitational-wave antenna, returning a cipher-like message and favor delta preview.";
        public string DisplayName => "未知文明通信";
        public int Level => 3;
        public string ParametersJson => JsonConvert.SerializeObject(new { type = "object", properties = new { }, required = new string[] { } });

        public string BuildToolJson()
        {
            var parameters = JsonConvert.DeserializeObject<object>(ParametersJson);
            return JsonConvert.SerializeObject(new { type = "function", function = new { name = Name, description = Description, parameters } });
        }
    }
}
