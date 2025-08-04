namespace RimAI.Core.Architecture.DI
{
    /// <summary>
    /// 全局服务定位器，仅在早期阶段使用；后续可用 DI 注入替代。
    /// </summary>
    public static class CoreServices
    {
        public static ServiceContainer Container { get; set; }

        public static T Resolve<T>() where T : class => Container?.Resolve<T>();
    }
}