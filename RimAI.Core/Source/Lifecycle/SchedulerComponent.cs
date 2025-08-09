using RimAI.Core.Infrastructure;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Modules.Embedding;
using Verse;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld 主线程调度泵。这是一个轻量级 <see cref="GameComponent"/>,
    /// 在每帧 <see cref="Update()"/> 中调用 <see cref="SchedulerService"/> 的 <c>Pump()</c>,
    /// 以便执行所有排队的后台任务。
    /// <para>仅管理调度行为，不持久化任何状态。</para>
    /// </summary>
    public class SchedulerComponent : GameComponent
    {
        private readonly SchedulerService _scheduler;
        private bool _toolIndexTickCheckTriggered;

        public SchedulerComponent(Game game) : base()
        {
            // DI must be ready before any GameComponent is constructed.
            _scheduler = (SchedulerService)Infrastructure.CoreServices.Locator.Get<ISchedulerService>();
        }

        public override void GameComponentTick()
        {
            try
            {
                // 每 Tick 泵出队列，执行所有主线程任务。
                _scheduler.Pump();

                // S2.5（最小提示）：如索引正在构建，定期提示一次（每600 tick）。
                // 后续可根据配置 BlockDuringBuild 决定是否阻断工具调用。
                if (Find.TickManager.TicksGame % 600 == 0)
                {
                    var toolIndex = Infrastructure.CoreServices.Locator.Get<RimAI.Core.Modules.Embedding.IToolVectorIndexService>();
                    if (toolIndex.IsBuilding)
                    {
                        Infrastructure.CoreServices.Logger.Info("[ToolIndex] Building tool vector index...");
                    }
                }

                // S2.5: 第 1000 Tick 触发一次自动构建检查（兼容无小人落地）
                // 仅在进入正式游戏（非新建存档初始化流程）后执行，避免主菜单/加载阶段触发
                if (!_toolIndexTickCheckTriggered && Find.TickManager.TicksGame == 1000 && Current.Game != null && Current.Game.InitData == null)
                {
                    try
                    {
                        var cfg = Infrastructure.CoreServices.Locator.Get<IConfigurationService>();
                        if (cfg?.Current?.Embedding?.Tools?.AutoBuildOnStart ?? true)
                        {
                            var toolIndex = Infrastructure.CoreServices.Locator.Get<IToolVectorIndexService>();
                            if (toolIndex != null && !toolIndex.IsReady && !toolIndex.IsBuilding)
                            {
                                _ = toolIndex.EnsureBuiltAsync();
                            }
                        }
                    }
                    catch { /* ignore */ }
                    finally { _toolIndexTickCheckTriggered = true; }
                }
            }
            catch (System.Exception ex)
            {
                Infrastructure.CoreServices.Logger.Warn($"[Scheduler] Pump error: {ex.Message}");
            }
        }
    }
}