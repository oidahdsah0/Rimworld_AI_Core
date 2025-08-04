using System;
#nullable enable

namespace RimAI.Core.Architecture.DI
{
    /// <summary>
    /// 全局服务定位器，仅限 UI/GameComponent 等无法构造函数注入的场景使用。
    /// </summary>
    public static class CoreServices
    {
        private static ServiceContainer? _container;

        public static ServiceContainer Container
        {
            get => _container ?? throw new InvalidOperationException("ServiceContainer 尚未初始化");
            set => _container = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static T Resolve<T>() where T : class => Container.Resolve<T>();
        public static bool TryResolve<T>(out T service) where T : class => Container.TryResolve(out service);
    }
}