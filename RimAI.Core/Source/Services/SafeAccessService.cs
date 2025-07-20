using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RimWorld;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// RimWorld API 安全访问服务 - 统一处理并发修改和空值异常
    /// 🎯 解决整个代码库中重复的安全访问代码问题
    /// 
    /// 核心功能：
    /// - 统一的重试机制处理 InvalidOperationException
    /// - 空值安全检查和默认值返回
    /// - 性能监控和错误日志
    /// - 标准化的异常处理流程
    /// </summary>
    public static class SafeAccessService
    {
        private static readonly Dictionary<string, int> _operationFailures = new Dictionary<string, int>();
        private static readonly object _statsLock = new object();

        #region 集合安全访问 - 核心方法

        /// <summary>
        /// 安全获取殖民者列表
        /// </summary>
        /// <param name="map">目标地图</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>安全的殖民者列表副本</returns>
        public static List<Pawn> GetColonistsSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() => 
                map?.mapPawns?.FreeColonists?.ToList() ?? new List<Pawn>(), 
                new List<Pawn>(), 
                maxRetries,
                "GetColonists"
            );
        }

        /// <summary>
        /// 安全获取囚犯列表
        /// </summary>
        public static List<Pawn> GetPrisonersSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.mapPawns?.PrisonersOfColony?.ToList() ?? new List<Pawn>(),
                new List<Pawn>(),
                maxRetries,
                "GetPrisoners"
            );
        }

        /// <summary>
        /// 安全获取所有生物列表
        /// </summary>
        public static List<Pawn> GetAllPawnsSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.mapPawns?.AllPawns?.ToList() ?? new List<Pawn>(),
                new List<Pawn>(),
                maxRetries,
                "GetAllPawns"
            );
        }

        /// <summary>
        /// 安全获取建筑列表
        /// </summary>
        public static List<Building> GetBuildingsSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.listerBuildings?.allBuildingsColonist?.ToList() ?? new List<Building>(),
                new List<Building>(),
                maxRetries,
                "GetBuildings"
            );
        }

        /// <summary>
        /// 安全获取特定类型物品
        /// </summary>
        public static List<Thing> GetThingsSafe(Map map, ThingDef thingDef, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.listerThings?.ThingsOfDef(thingDef)?.ToList() ?? new List<Thing>(),
                new List<Thing>(),
                maxRetries,
                $"GetThings({thingDef?.defName ?? "null"})"
            );
        }

        /// <summary>
        /// 安全获取物品组
        /// </summary>
        public static List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.listerThings?.ThingsInGroup(group)?.ToList() ?? new List<Thing>(),
                new List<Thing>(),
                maxRetries,
                $"GetThingGroup({group})"
            );
        }

        #endregion

        #region 单个对象安全访问

        /// <summary>
        /// 安全获取殖民者数量
        /// </summary>
        public static int GetColonistCountSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.mapPawns?.FreeColonistsCount ?? 0,
                0,
                maxRetries,
                "GetColonistCount"
            );
        }

        /// <summary>
        /// 安全获取天气信息
        /// </summary>
        public static WeatherDef GetCurrentWeatherSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                map?.weatherManager?.curWeather,
                null,
                maxRetries,
                "GetCurrentWeather"
            );
        }

        /// <summary>
        /// 安全获取游戏时间
        /// </summary>
        public static int GetTicksGameSafe(int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                Find.TickManager?.TicksGame ?? 0,
                0,
                maxRetries,
                "GetTicksGame"
            );
        }

        /// <summary>
        /// 安全获取季节信息
        /// </summary>
        public static Season GetCurrentSeasonSafe(Map map, int maxRetries = 3)
        {
            return ExecuteWithRetry(() =>
                GenLocalDate.Season(map),
                Season.Spring,
                maxRetries,
                "GetCurrentSeason"
            );
        }

        #endregion

        #region 批量操作安全方法

        /// <summary>
        /// 安全执行Pawn集合操作
        /// </summary>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="pawns">Pawn列表</param>
        /// <param name="operation">要执行的操作</param>
        /// <param name="fallbackValue">失败时的默认值</param>
        /// <param name="operationName">操作名称（用于日志）</param>
        /// <returns>操作结果或默认值</returns>
        public static TResult SafePawnOperation<TResult>(
            List<Pawn> pawns,
            Func<List<Pawn>, TResult> operation,
            TResult fallbackValue,
            string operationName)
        {
            try
            {
                var safePawns = pawns ?? new List<Pawn>();
                return operation(safePawns);
            }
            catch (Exception ex)
            {
                Log.Warning($"[SafeAccess] Pawn operation '{operationName}' failed: {ex.Message}");
                RecordOperationFailure(operationName);
                return fallbackValue;
            }
        }

        /// <summary>
        /// 安全执行Building集合操作
        /// </summary>
        public static TResult SafeBuildingOperation<TResult>(
            List<Building> buildings,
            Func<List<Building>, TResult> operation,
            TResult fallbackValue,
            string operationName)
        {
            try
            {
                var safeBuildings = buildings ?? new List<Building>();
                return operation(safeBuildings);
            }
            catch (Exception ex)
            {
                Log.Warning($"[SafeAccess] Building operation '{operationName}' failed: {ex.Message}");
                RecordOperationFailure(operationName);
                return fallbackValue;
            }
        }

        /// <summary>
        /// 安全执行Thing集合操作
        /// </summary>
        public static TResult SafeThingOperation<TResult>(
            List<Thing> things,
            Func<List<Thing>, TResult> operation,
            TResult fallbackValue,
            string operationName)
        {
            try
            {
                var safeThings = things ?? new List<Thing>();
                return operation(safeThings);
            }
            catch (Exception ex)
            {
                Log.Warning($"[SafeAccess] Thing operation '{operationName}' failed: {ex.Message}");
                RecordOperationFailure(operationName);
                return fallbackValue;
            }
        }

        #endregion

        #region 核心重试机制

        /// <summary>
        /// 通用重试执行器 - 处理所有RimWorld API访问的核心方法
        /// 
        /// 处理的异常类型：
        /// - InvalidOperationException: 集合在枚举期间被修改
        /// - NullReferenceException: 空引用访问
        /// - ArgumentException: 参数错误
        /// </summary>
        /// <typeparam name="T">返回类型</typeparam>
        /// <param name="operation">要执行的操作</param>
        /// <param name="fallbackValue">失败时返回的默认值</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="operationName">操作名称（用于日志和统计）</param>
        /// <returns>操作结果或默认值</returns>
        private static T ExecuteWithRetry<T>(
            Func<T> operation,
            T fallbackValue,
            int maxRetries,
            string operationName)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    return operation();
                }
                catch (InvalidOperationException ex) when (attempt < maxRetries)
                {
                    // 集合被修改 - 这是最常见的并发问题
                    Log.Warning($"[SafeAccess] {operationName} attempt {attempt} failed (collection modified): {ex.Message}");
                    
                    // 短暂延迟让游戏主线程完成操作
                    Thread.Sleep(1);
                    continue;
                }
                catch (NullReferenceException ex) when (attempt < maxRetries)
                {
                    // 空引用 - 可能是对象被销毁
                    Log.Warning($"[SafeAccess] {operationName} attempt {attempt} failed (null reference): {ex.Message}");
                    Thread.Sleep(1);
                    continue;
                }
                catch (ArgumentException ex) when (attempt < maxRetries)
                {
                    // 参数错误 - 可能是游戏状态不一致
                    Log.Warning($"[SafeAccess] {operationName} attempt {attempt} failed (argument error): {ex.Message}");
                    Thread.Sleep(1);
                    continue;
                }
                catch (Exception ex)
                {
                    // 其他未预期的异常 - 直接失败，不重试
                    Log.Error($"[SafeAccess] {operationName} failed with unexpected error: {ex.GetType().Name}: {ex.Message}");
                    RecordOperationFailure(operationName);
                    return fallbackValue;
                }
            }

            // 所有重试都失败了
            Log.Error($"[SafeAccess] {operationName} failed after {maxRetries} attempts, returning fallback value");
            RecordOperationFailure(operationName);
            return fallbackValue;
        }

        #endregion

        #region 统计和监控

        /// <summary>
        /// 记录操作失败次数
        /// </summary>
        private static void RecordOperationFailure(string operationName)
        {
            lock (_statsLock)
            {
                if (_operationFailures.ContainsKey(operationName))
                {
                    _operationFailures[operationName]++;
                }
                else
                {
                    _operationFailures[operationName] = 1;
                }
            }
        }

        /// <summary>
        /// 获取失败统计信息
        /// </summary>
        public static Dictionary<string, int> GetFailureStats()
        {
            lock (_statsLock)
            {
                return new Dictionary<string, int>(_operationFailures);
            }
        }

        /// <summary>
        /// 清除统计信息
        /// </summary>
        public static void ClearStats()
        {
            lock (_statsLock)
            {
                _operationFailures.Clear();
            }
        }

        /// <summary>
        /// 获取服务状态报告
        /// </summary>
        public static string GetStatusReport()
        {
            lock (_statsLock)
            {
                if (_operationFailures.Count == 0)
                {
                    return "SafeAccessService: 运行正常，无失败记录";
                }

                var totalFailures = _operationFailures.Values.Sum();
                var report = $"SafeAccessService: 总失败次数 {totalFailures}\n";
                
                foreach (var kvp in _operationFailures.OrderByDescending(x => x.Value))
                {
                    report += $"  {kvp.Key}: {kvp.Value} 次失败\n";
                }

                return report.TrimEnd('\n');
            }
        }

        #endregion
    }
}
