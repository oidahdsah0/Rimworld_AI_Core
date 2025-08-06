using RimAI.Core.Infrastructure;
using Verse;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld 主线程调度泵。这是一个轻量级 <see cref="GameComponent"/>，
    /// 在每帧 <see cref="Update()"/> 中调用 <see cref="SchedulerService"/> 的 <c>Pump()</c>，
    /// 以便执行所有排队的后台任务。
    /// <para>仅管理调度行为，不持久化任何状态。</para>
    /// </summary>
    public class SchedulerComponent : GameComponent
    {
        private readonly SchedulerService _scheduler;

        public SchedulerComponent(Game game) : base()
        {
            // DI must be ready before any GameComponent is constructed.
            _scheduler = (SchedulerService)Infrastructure.CoreServices.Locator.Get<ISchedulerService>();
        }

        public override void GameComponentTick()
        {
            // 每 Tick 泵出队列，执行所有主线程任务。
            _scheduler.Pump();
        }
    }
}