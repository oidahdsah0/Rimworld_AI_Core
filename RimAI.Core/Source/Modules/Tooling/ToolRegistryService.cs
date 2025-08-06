using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Modules.Tooling
{
    /// <summary>
    /// 默认的工具注册与执行服务 (P4)。
    /// 在服务构造时扫描所有已加载程序集，
    /// 自动发现并实例化实现 <see cref="IRimAITool"/> 的类型。
    /// </summary>
    internal sealed class ToolRegistryService : IToolRegistryService
    {
        private readonly ConcurrentDictionary<string, IRimAITool> _tools = new(StringComparer.OrdinalIgnoreCase);

        public ToolRegistryService()
        {
            DiscoverTools();
        }

        #region IToolRegistryService

        public List<ToolFunction> GetAllToolSchemas()
        {
            return _tools.Values.Select(t => t.GetSchema()).ToList();
        }

        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentNullException(nameof(toolName));
            if (!_tools.TryGetValue(toolName, out var tool))
                throw new InvalidOperationException($"[RimAI] Tool '{toolName}' 未注册。");
            // 直接调用工具实现
            return await tool.ExecuteAsync(parameters ?? new Dictionary<string, object>());
        }

        #endregion

        #region Private Methods

        private void DiscoverTools()
        {
            // 扫描当前 AppDomain 的所有程序集（排除系统程序集以提升性能）
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.FullName.StartsWith("System") && !a.FullName.StartsWith("Unity"));

            foreach (var asm in assemblies)
            {
                RegisterToolsFromAssembly(asm);
            }
        }

        private void RegisterToolsFromAssembly(Assembly assembly)
        {
            IEnumerable<Type> types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null)!;
            }

            foreach (var type in types)
            {
                if (!typeof(IRimAITool).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    continue;

                // 使用 ServiceContainer 构造，以便依赖注入
                IRimAITool instance = null;
                try
                {
                    // 尝试通过 DI 容器构造，以支持依赖注入
                    var genericGet = typeof(ServiceContainer).GetMethod("Get")?.MakeGenericMethod(type);
                    if (genericGet != null)
                        instance = (IRimAITool)genericGet.Invoke(null, null);
                }
                catch
                {
                    // Ignore – fallback to reflection below
                }

                // 若 DI 构造失败，降级使用无参构造
                instance ??= Activator.CreateInstance(type) as IRimAITool;

                if (instance == null) continue;
                _tools[instance.Name] = instance;
                CoreServices.Logger.Info($"Tool registered: {instance.Name}");
            }
        }

        #endregion
    }
}
