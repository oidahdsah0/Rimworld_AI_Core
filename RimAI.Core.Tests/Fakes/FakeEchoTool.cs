using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Tools;
using RimAI.Framework.Contracts;

namespace RimAI.Core.Tests.Fakes
{
    internal class FakeEchoTool : IRimAITool
    {
        public string Name => "echo";
        public string Description => "返回参数 text。";

        public ToolDefinition GetSchema()
        {
            return new ToolDefinition
            {
                Function = Newtonsoft.Json.Linq.JObject.Parse("{\"name\":\"echo\",\"parameters\":{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}}}}")
            };
        }

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("text", out var val))
            {
                return Task.FromResult<object>(val?.ToString() ?? string.Empty);
            }
            return Task.FromResult<object>(string.Empty);
        }
    }
}
