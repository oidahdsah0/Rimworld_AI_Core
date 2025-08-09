using RimAI.Core.Infrastructure;
using Verse;
using UnityEngine;
using RimAI.Core.UI.Settings;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld 会在 Mod 加载时实例化继承自 <see cref="Verse.Mod"/> 的类。
    /// 我们在此初始化依赖注入容器，并输出 Skeleton 启动日志。
    /// </summary>
    public class RimAIMod : Mod
    {
        private CoreSettingsPanel _panel;

        public RimAIMod(ModContentPack content) : base(content)
        {
            // 初始化依赖注入容器（P0 手动注册）。
            ServiceContainer.Init();

            // 初始化设置面板
            _panel = new CoreSettingsPanel();

            // S2.5：确保工具向量索引存在（首次构建）。仅在进入正式游戏后触发，避免主菜单阶段大量构建与日志刷屏。
            try
            {
                var toolIndex = CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                if ((cfg?.Current?.Embedding?.Tools?.AutoBuildOnStart ?? true) && toolIndex != null)
                {
                    // 仅在已进入游戏且不是新建流程时尝试构建；主菜单阶段交由 SchedulerComponent 在1000Tick检查
                    if (Current.Game != null && Current.Game.InitData == null && !toolIndex.IsReady && !toolIndex.IsBuilding)
                    {
                        _ = toolIndex.EnsureBuiltAsync();
                    }
                }
            }
            catch { /* 允许继续启动 */ }

            // 输出启动日志，作为 P0 Gate 验收指标之一。
            // 初始化事件聚合器，开始订阅并启动定时器（增加防护，避免初始化异常导致崩溃）
            try
            {
                var aggregator = CoreServices.Locator.Get<RimAI.Core.Contracts.Eventing.IEventAggregatorService>();
                aggregator?.Initialize();
            }
            catch (System.Exception ex)
            {
                CoreServices.Logger.Warn($"[RimAI] EventAggregator initialize failed: {ex.Message}");
            }

            CoreServices.Logger.Info("RimAI v4 Skeleton Loaded");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            _panel?.Draw(inRect);
        }

        public override string SettingsCategory() => "RimAI Core";
    }
}