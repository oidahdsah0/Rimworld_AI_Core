using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Events;
using RimAI.Core.Services;
using RimAI.Core.Settings;
using RimAI.Framework.API;
using RimWorld;
using System.Threading.Tasks;
using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core 游戏组件
    /// 负责初始化新架构和框架连接测试
    /// </summary>
    public class RimAICoreGameComponent : GameComponent
    {
        public RimAICoreGameComponent(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            CoreServices.PersistenceService?.Load(); // Corrected to PersistenceService
            Log.Message("[RimAI.Core] Game loaded. Persistence state restored.");
        }

        public override void ExposeData()
        {
            base.ExposeData();
            CoreServices.PersistenceService?.Save(); // Corrected to PersistenceService
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();

            // Check service readiness every 2 seconds (120 ticks)
            if (Find.TickManager.TicksGame % 120 == 0)
            {
                if (!CoreServices.AreServicesReady())
                {
                    Log.WarningOnce("[RimAI.Core] One or more services are not ready. Please check settings.", GetHashCode());
                }
            }
        }
    }
}
