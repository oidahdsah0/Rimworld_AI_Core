using System;
using RimAI.Core.Settings;

namespace RimAI.Core.Infrastructure.Configuration
{
    /// <summary>
    /// 全局配置服务接口。P1 提供热重载事件，后续阶段可能扩展更多能力。
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 当前激活的不可变配置对象。
        /// </summary>
        CoreConfig Current { get; }

        /// <summary>
        /// 当配置重新加载后触发。
        /// </summary>
        event Action<CoreConfig> OnConfigurationChanged;

        /// <summary>
        /// 重新加载配置。P1 版本直接重置为默认值。
        /// </summary>
        void Reload();

        /// <summary>
        /// 应用一份新的配置快照并广播变更事件。
        /// </summary>
        /// <param name="snapshot">新的不可变配置对象。</param>
        void Apply(CoreConfig snapshot);
    }
}