using Verse;

namespace RimAI.Core.Infrastructure
{
    /// <summary>
    /// 对外暴露受限的静态服务定位器。仅在 RimWorld 引擎无法通过构造函数注入的场景下使用。
    /// 在 P0 仅提供 Logger 及 ServiceContainer 访问。
    /// </summary>
    public static class CoreServices
    {
        /// <summary>
        /// 访问底层 <see cref="ServiceContainer"/> 的快捷入口。
        /// 仅供无法注入的 RimWorld 对象极少量调用。
        /// </summary>
        public static class Locator
        {
            public static T Get<T>() where T : class => ServiceContainer.Get<T>();
        }

        /// <summary>
        /// 统一日志入口，暂时直接包装 <see cref="Log"/>。
        /// 未来可替换为自定义带等级的日志系统。
        /// </summary>
        public static class Logger
        {
            public static void Info(string message) => Log.Message($"[RimAI] {message}");
            public static void Warn(string message) => Log.Warning($"[RimAI] {message}");
            public static void Error(string message) => Log.Error($"[RimAI] {message}");
        }
    }
}