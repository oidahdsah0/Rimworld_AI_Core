using RimAI.Core.Contracts.Services;

namespace RimAI.Core.Architecture.DI
{
    public static class CoreService
    {
        /// <ServiceContainer>
        /// internal vs public: internal 意味着 Container 属性只能在 RimAI.Core 这个项目内部被访问，
        /// 这其实是一个更安全、更规范的设计，它防止了其他外部模块（如果有的话）意外地修改我们的容器。
        /// </ServiceContainer>
        internal static ServiceContainer Container { get; set; }

        public static IConfigurationService Configuration => Container.Resolve<IConfigurationService>();
    }
}