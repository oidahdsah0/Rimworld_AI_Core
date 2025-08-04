using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Contracts.Tools;
using RimAI.Framework.Contracts;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 自动发现并管理 IRimAITool 的注册中心。
    /// </summary>
    public class ToolRegistryService : IToolRegistryService
    {
        private readonly Dictionary<string, IRimAITool> _tools = new();

        public ToolRegistryService(IEnumerable<IRimAITool> toolInstances)
        {
            foreach (var tool in toolInstances)
            {
                _tools[tool.Name] = tool;
                Log.Message($"[RimAI.ToolRegistry] Registered tool '{tool.Name}'.");
            }
        }

        public List<ToolDefinition> GetAllToolSchemas()
        {
            return _tools.Values.Select(t => t.GetSchema()).ToList();
        }

        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                throw new InvalidOperationException($"Tool '{toolName}' not found.");
            }
            return await tool.ExecuteAsync(parameters);
        }

        // 帮助方法：采用反射扫描程序集生成工具实例列表
        public static IEnumerable<IRimAITool> DiscoverTools(IServiceProvider sp)
        {
            var toolType = typeof(IRimAITool);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && toolType.IsAssignableFrom(t));

            foreach (var type in types)
            {
                // 简易：仅尝试参数最少的构造函数
                var ctor = type.GetConstructors().OrderBy(c => c.GetParameters().Length).FirstOrDefault();
                if (ctor == null) continue;

                var args = ctor.GetParameters()
                    .Select(p => sp.GetService(p.ParameterType))
                    .ToArray();
                yield return (IRimAITool)Activator.CreateInstance(type, args);
            }
        }
    }
}