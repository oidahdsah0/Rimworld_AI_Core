using RimAI.Core.Architecture.DI;
using RimAI.Core.Contracts.Services;
using Verse;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// 发令员：挂接 RimWorld 存档流程，转发到 IPersistenceService。
    /// </summary>
    public class PersistenceManager : GameComponent
    {
        private readonly IPersistenceService _persistence;
        private readonly IHistoryService _history;

        public PersistenceManager()
        {
            // 通过 CoreServices 解析依赖
            _persistence = CoreServices.Container.Resolve<IPersistenceService>();
            _history = CoreServices.Container.Resolve<IHistoryService>();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            _persistence.PersistHistoryState(_history);
        }
    }
}
