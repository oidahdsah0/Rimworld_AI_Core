using RimAI.Core.Contracts.Services;
using RimAI.Core.Events;
using Verse;

namespace RimAI.Core.Events
{
    /// <summary>
    /// RimWorld GameComponent：每 1000 tick 发布一次 TickEvent，用作测试。
    /// </summary>
    public class GameEventHooks : GameComponent
    {
        private int _lastPublishedTick;
        private IEventBus _bus => RimAI.Core.Architecture.DI.CoreServices.Container.Resolve<IEventBus>();

        public GameEventHooks() { }

        public override void GameComponentTick()
        {
            var tick = Find.TickManager.TicksGame;
            if (tick - _lastPublishedTick >= 1000)
            {
                _lastPublishedTick = tick;
                _bus.Publish(new TickEvent(tick));
            }
        }
    }
}
