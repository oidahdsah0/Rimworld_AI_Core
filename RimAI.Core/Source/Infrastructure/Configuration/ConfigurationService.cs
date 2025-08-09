using System;
using RimAI.Core.Settings;

namespace RimAI.Core.Infrastructure.Configuration
{
    /// <summary>
    /// P1 版本的配置服务：读取 RimWorld 的 ModSettings 将在后续阶段实现。
    /// 目前返回默认 <see cref="CoreConfig"/>，并支持 Hot Reload 事件广播。
    /// </summary>
    public sealed class ConfigurationService : IConfigurationService
    {
        private CoreConfig _current = CoreConfig.CreateDefault();
        public CoreConfig Current => _current;

        public event Action<CoreConfig> OnConfigurationChanged;

        public void Reload()
        {
            // TODO: RimWorld 设置读取逻辑（P3 或更高阶段）
            _current = CoreConfig.CreateDefault();
            OnConfigurationChanged?.Invoke(_current);

            // 配置变更后触发工具索引重建（标记过期并尝试异步构建）
            try
            {
                var index = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                index?.MarkStale();
                _ = index?.EnsureBuiltAsync();
            }
            catch { /* ignore */ }
        }

        public void Apply(CoreConfig snapshot)
        {
            if (snapshot == null) return;
            _current = snapshot;
            OnConfigurationChanged?.Invoke(_current);

            // 配置变更后触发工具索引重建
            try
            {
                var index = RimAI.Core.Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                index?.MarkStale();
                _ = index?.EnsureBuiltAsync();
            }
            catch { /* ignore */ }
        }
    }
}