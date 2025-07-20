using System;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services.Examples
{
    /// <summary>
    /// CacheService使用示例和测试类
    /// </summary>
    public class CacheServiceDemonstrator
    {
        private readonly ICacheService _cache;

        public CacheServiceDemonstrator()
        {
            // 通过企业级架构获取缓存服务
            _cache = CoreServices.CacheService;
        }

        /// <summary>
        /// 演示缓存基本用法
        /// </summary>
        public async Task DemonstrateBasicUsage()
        {
            Log.Message("=== CacheService Basic Usage Demo ===");

            // 1. 基本缓存操作
            var expensiveData = await _cache.GetOrCreateAsync(
                "expensive_calculation",
                async () => {
                    Log.Message("[Cache Demo] Performing expensive calculation...");
                    await Task.Delay(1000); // 模拟耗时操作
                    return "Computed Result: " + DateTime.Now.ToString();
                },
                TimeSpan.FromMinutes(5)
            );

            Log.Message($"[Cache Demo] First call result: {expensiveData}");

            // 2. 再次调用同样的key - 应该直接从缓存返回
            var cachedData = await _cache.GetOrCreateAsync(
                "expensive_calculation",
                () => {
                    Log.Message("[Cache Demo] This should NOT be called!");
                    return Task.FromResult("Should not see this");
                }
            );

            Log.Message($"[Cache Demo] Second call result (from cache): {cachedData}");
        }

        /// <summary>
        /// 演示Governor建议的缓存机制
        /// </summary>
        public async Task DemonstrateGovernorAdviceCache()
        {
            Log.Message("=== Governor Advice Cache Demo ===");

            var governor = CoreServices.Governor;
            if (governor == null)
            {
                Log.Warning("[Cache Demo] Governor not available");
                return;
            }

            // 测试Governor建议的缓存
            var query = "当前殖民地状况如何？";
            
            // 第一次请求 - 应该会调用LLM
            var startTime = DateTime.Now;
            var advice1 = await governor.HandleUserQueryAsync(query);
            var firstCallTime = (DateTime.Now - startTime).TotalMilliseconds;
            
            Log.Message($"[Cache Demo] First Governor call took {firstCallTime}ms");
            
            // 第二次相同请求 - 应该从缓存返回（如果有缓存逻辑）
            startTime = DateTime.Now;
            var advice2 = await governor.HandleUserQueryAsync(query);
            var secondCallTime = (DateTime.Now - startTime).TotalMilliseconds;
            
            Log.Message($"[Cache Demo] Second Governor call took {secondCallTime}ms");
            Log.Message($"[Cache Demo] Performance improvement: {(firstCallTime > secondCallTime ? "✅" : "⚠️")}");
        }

        /// <summary>
        /// 演示缓存的过期和清理机制
        /// </summary>
        public async Task DemonstrateCacheExpiration()
        {
            Log.Message("=== Cache Expiration Demo ===");

            // 创建短期缓存项
            var shortLivedData = await _cache.GetOrCreateAsync(
                "short_lived_data",
                () => {
                    Log.Message("[Cache Demo] Creating short-lived data");
                    return Task.FromResult("Temporary Data: " + DateTime.Now.ToString());
                },
                TimeSpan.FromSeconds(2) // 2秒过期
            );

            Log.Message($"[Cache Demo] Created short-lived data: {shortLivedData}");

            // 等待3秒，让缓存过期
            await Task.Delay(3000);

            // 再次请求 - 应该重新创建
            var newData = await _cache.GetOrCreateAsync(
                "short_lived_data",
                () => {
                    Log.Message("[Cache Demo] Recreating expired data");
                    return Task.FromResult("New Data: " + DateTime.Now.ToString());
                },
                TimeSpan.FromSeconds(2)
            );

            Log.Message($"[Cache Demo] After expiration: {newData}");
        }

        /// <summary>
        /// 显示缓存统计信息
        /// </summary>
        public void ShowCacheStats()
        {
            Log.Message("=== Cache Statistics ===");

            if (_cache is CacheService concreteCache)
            {
                var stats = concreteCache.GetStats();
                
                Log.Message($"[Cache Stats] Total Entries: {stats.TotalEntries}");
                Log.Message($"[Cache Stats] Active Entries: {stats.ActiveEntries}");
                Log.Message($"[Cache Stats] Expired Entries: {stats.ExpiredEntries}");
                Log.Message($"[Cache Stats] Total Access Count: {stats.TotalAccessCount}");
                Log.Message($"[Cache Stats] Max Entries: {stats.MaxEntries}");
                Log.Message($"[Cache Stats] Default Expiration: {stats.DefaultExpiration}");
            }
            else
            {
                Log.Message("[Cache Stats] Cannot access concrete implementation stats");
            }
        }

        /// <summary>
        /// 运行完整的缓存演示
        /// </summary>
        public async Task RunFullDemo()
        {
            try
            {
                await DemonstrateBasicUsage();
                await Task.Delay(1000);
                
                await DemonstrateGovernorAdviceCache();
                await Task.Delay(1000);
                
                await DemonstrateCacheExpiration();
                await Task.Delay(1000);
                
                ShowCacheStats();
                
                Log.Message("=== Cache Demo Complete ===");
            }
            catch (Exception ex)
            {
                Log.Error($"[Cache Demo] Error: {ex.Message}");
            }
        }
    }
}
