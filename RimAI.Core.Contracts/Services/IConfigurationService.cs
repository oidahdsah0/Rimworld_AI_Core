using RimAI.Core.Contracts.Settings;

namespace RimAI.Core.Contracts.Services
{
    /// <summary>
    /// 对外暴露的只读配置接口。提供当前不可变配置快照。
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 当前激活的不可变配置快照。
        /// </summary>
        CoreConfigSnapshot Current { get; }
    }
}


