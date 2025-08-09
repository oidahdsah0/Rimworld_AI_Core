using RimAI.Core.Infrastructure;
using Verse;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld 会在 Mod 加载时实例化继承自 <see cref="Verse.Mod"/> 的类。
    /// 我们在此初始化依赖注入容器，并输出 Skeleton 启动日志。
    /// </summary>
    public class RimAIMod : Mod
    {
        public RimAIMod(ModContentPack content) : base(content)
        {
            // 初始化依赖注入容器（P0 手动注册）。
            ServiceContainer.Init();

            // S2.5：确保工具向量索引存在（首次构建）。构建期间可阻断工具功能（后续接入）。
            try
            {
                var toolIndex = CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                if (cfg?.Current?.Embedding?.Tools?.AutoBuildOnStart ?? true)
                {
                    // 异步构建，避免阻塞 RimWorld 构造流。后续可改为落地时机更靠后（小人落地）。
                    _ = toolIndex.EnsureBuiltAsync();
                }
            }
            catch { /* 允许继续启动 */ }

            // 输出启动日志，作为 P0 Gate 验收指标之一。
            // 初始化事件聚合器，开始订阅并启动定时器
            var aggregator = CoreServices.Locator.Get<RimAI.Core.Contracts.Eventing.IEventAggregatorService>();
            aggregator.Initialize();

            CoreServices.Logger.Info("RimAI v4 Skeleton Loaded");
        }
    }
}