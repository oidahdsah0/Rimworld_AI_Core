using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RimAI.Framework.Contracts;
using RimAI.Core.Contracts.Tooling;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.Embedding;

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
            // 构建包含 Description 的完整 schema
            return _tools.Values.Select(t => 
            {
                var schema = t.GetSchema();
                if (schema == null)
                {
                    // 回退到最小 schema
                    return new ToolFunction
                    {
                        Name = t.Name,
                        Description = t.Description ?? string.Empty,
                        Arguments = "{}"
                    };
                }
                // 如果 schema 没有设置 Description，从工具本身获取
                if (string.IsNullOrWhiteSpace(schema.Description))
                {
                    schema.Description = t.Description;
                }
                if (string.IsNullOrWhiteSpace(schema.Name))
                {
                    schema.Name = t.Name;
                }
                if (string.IsNullOrWhiteSpace(schema.Arguments))
                {
                    schema.Arguments = "{}";
                }
                return schema;
            }).ToList();
        }

        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            if (string.IsNullOrWhiteSpace(toolName))
                throw new ArgumentNullException(nameof(toolName));
            if (!_tools.TryGetValue(toolName, out var tool))
                throw new InvalidOperationException($"[RimAI] Tool '{toolName}' 未注册。");

            // S2.5（可选阻断）：如果配置开启在索引构建期间阻断工具调用，则进行拦截
            try
            {
                var cfg = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                var block = cfg?.Current?.Embedding?.Tools != null &&
                            (cfg.Current.Embedding.Tools.GetType().GetProperty("BlockDuringBuild")?.GetValue(cfg.Current.Embedding.Tools) as bool? ?? false);
                if (block)
                {
                    var toolIndex = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                    if (toolIndex != null && toolIndex.IsBuilding)
                        throw new InvalidOperationException("工具向量索引正在构建，暂不可用（可在设置中关闭阻断）。");
                }
            }
            catch { /* 忽略阻断检查异常，避免影响工具执行 */ }
            try
            {
                return await tool.ExecuteAsync(parameters ?? new Dictionary<string, object>());
            }
            catch (System.Exception ex)
            {
                throw new InvalidOperationException($"[RimAI] Tool '{toolName}' 执行失败: {ex.Message}", ex);
            }
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

            // 去重：同程序集内的同名工具类型仅处理一次
            var handledTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var type in types)
            {
                if (!typeof(IRimAITool).IsAssignableFrom(type) || type.IsAbstract || type.IsInterface)
                    continue;
                var typeKey = type.FullName ?? type.Name;
                if (!handledTypes.Add(typeKey)) continue;

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

                // 若 DI 构造失败，降级使用无参构造（若仍失败则跳过该类型）
                if (instance == null)
                {
                    try
                    {
                        instance = Activator.CreateInstance(type) as IRimAITool;
                    }
                    catch (Exception ex)
                    {
                        CoreServices.Logger.Warn($"[ToolRegistry] Skip tool type {type.FullName}: {ex.Message}");
                        continue;
                    }
                }

                if (instance == null) continue;
                // 名称级去重：若重复命名则发出警告并跳过
                if (string.IsNullOrWhiteSpace(instance.Name))
                {
                    CoreServices.Logger.Warn($"[ToolRegistry] Tool without name: {type.FullName}");
                    continue;
                }
                if (_tools.ContainsKey(instance.Name))
                {
                    CoreServices.Logger.Warn($"[ToolRegistry] Duplicate tool name skipped: {instance.Name} ({type.FullName})");
                    continue;
                }

                if (_tools.TryAdd(instance.Name, instance))
                {
                    CoreServices.Logger.Info($"Tool registered: {instance.Name}");
                }

                // 标记工具向量索引为过期（仅在实例已存在时，避免构造期触发循环依赖）
                try
                {
                    if (ServiceContainer.TryGetExisting<IToolVectorIndexService>(out var index))
                    {
                        index.MarkStale();
                    }
                }
                catch { /* ignore */ }
            }
        }

        #endregion
    }
}
