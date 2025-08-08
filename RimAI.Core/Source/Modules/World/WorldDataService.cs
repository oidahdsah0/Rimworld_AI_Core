using System.Threading.Tasks;
using RimAI.Core.Infrastructure;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimAI.Core.Modules.World
{
    /// <summary>
    /// RimWorld 数据访问防腐层的初版实现（P3）。
    /// 当前仅暴露 <see cref="GetPlayerNameAsync"/> 一个示例方法。
    /// 通过 <see cref="ISchedulerService"/> 保证所有 RimWorld API 调用在主线程执行，
    /// 避免跨线程崩溃。
    /// </summary>
    public interface IWorldDataService
    {
        /// <summary>
        /// 异步获取玩家派系名称。
        /// </summary>
        Task<string> GetPlayerNameAsync();

        /// <summary>
        /// 获取殖民地的简要摘要（示例实现：殖民者数量、资源堆叠等）。
        /// </summary>
        Task<ColonySummary> GetColonySummaryAsync();
    }

    /// <summary>
    /// 用于返回给工具层的殖民地摘要数据结构。
    /// </summary>
    public class ColonySummary
    {
        public int ColonistCount { get; set; }
        public int FoodStockpile { get; set; }
        public string ThreatLevel { get; set; }
    }

    internal sealed class WorldDataService : IWorldDataService
    {
        private readonly ISchedulerService _scheduler;

        public WorldDataService(ISchedulerService scheduler)
        {
            _scheduler = scheduler;
        }

        /// <inheritdoc />
        public Task<string> GetPlayerNameAsync()
        {
            // 使用调度器在主线程执行 RimWorld API 调用。
            return _scheduler.ScheduleOnMainThreadAsync(() => Faction.OfPlayer?.Name ?? string.Empty);
        }

        /// <inheritdoc />
        public Task<ColonySummary> GetColonySummaryAsync()
        {
            return _scheduler.ScheduleOnMainThreadAsync(() =>
            {
                try
                {
                    var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned?.Count ?? 0;
                    // 粗略计算粮食堆叠（仅演示）
                    var food = ThingSetMaker_MakeThingList();
                    int foodCount = food?.Sum(t => t?.stackCount ?? 0) ?? 0;
                    var threat = colonists < 6 ? "Low" : "High";

                    return new ColonySummary
                    {
                        ColonistCount = colonists,
                        FoodStockpile = foodCount,
                        ThreatLevel = threat
                    };
                }
                catch
                {
                    return new ColonySummary
                    {
                        ColonistCount = 0,
                        FoodStockpile = 0,
                        ThreatLevel = "Unknown"
                    };
                }
            });
        }

        private static List<Thing> ThingSetMaker_MakeThingList()
        {
            var list = new List<Thing>();
            if (Find.Maps == null) return list;
            foreach (var map in Find.Maps)
            {
                if (map?.listerThings?.AllThings == null) continue;
                list.AddRange(map.listerThings.AllThings.Where(t => t?.def != null && t.def.IsNutritionGivingIngestible));
            }
            return list;
        }
    }
}