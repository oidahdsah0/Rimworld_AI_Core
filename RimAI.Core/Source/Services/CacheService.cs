using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using RimAI.Core.Settings;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 缓存服务实现 - 提供智能缓存功能，与Framework配置系统统一
    /// </summary>
    public class CacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new ConcurrentDictionary<string, CacheItem>();
        private readonly object _cleanupLock = new object();
        private readonly Timer _cleanupTimer;
        private readonly Timer _statsTimer;

        // 统计信息
        private long _totalRequests;
        private long _cacheHits;
        private long _cacheMisses;
        private long _evictions;
        private long _expirations;

        // 配置缓存 - 避免每次都读取配置
        private int _maxEntries;
        private TimeSpan _defaultExpiration;
        private TimeSpan _cleanupInterval;
        private int _maxMemoryMB;
        private double _minHitRate;
        private DateTime _lastConfigUpdate = DateTime.MinValue;

        public CacheService()
        {
            // 将 RefreshConfiguration 从构造函数中移除，避免在初始化时就依赖Framework
            SetSafeDefaults(); // 先使用安全的默认值
            
            // 初始化定时器
            _cleanupTimer = new Timer(
                CleanupExpiredEntries,
                null,
                _cleanupInterval,
                _cleanupInterval
            );
            
            _statsTimer = new Timer(
                LogStatistics,
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5)
            );

            Log.Message($"[CacheService] Initialized with safe defaults. Configuration will be loaded on first use.");
        }

        /// <summary>
        /// 刷新配置设置 - 从Framework配置系统读取
        /// </summary>
        private void RefreshConfiguration()
        {
            try
            {
                // 尝试从Framework配置系统读取
                var frameworkConfig = GetFrameworkConfiguration();
                if (frameworkConfig != null)
                {
                    _maxEntries = Math.Max(1, frameworkConfig.Get<int>("cache.size", 200));
                    _defaultExpiration = TimeSpan.FromMinutes(Math.Max(1, frameworkConfig.Get<int>("cache.ttlMinutes", 15)));
                    _cleanupInterval = TimeSpan.FromMinutes(Math.Max(1, frameworkConfig.Get<int>("cache.cleanupIntervalMinutes", 1)));
                    _maxMemoryMB = Math.Max(10, frameworkConfig.Get<int>("cache.maxMemoryMB", 200));
                    _minHitRate = Math.Max(0.05, frameworkConfig.Get<double>("cache.minHitRate", 0.1));
                }
                else
                {
                    // 回退到Core配置
                    var coreSettings = SettingsManager.Settings?.Cache;
                    if (coreSettings != null)
                    {
                        _maxEntries = Math.Max(1, Math.Min(coreSettings.MaxCacheEntries, 500)); // 限制最大值
                        _defaultExpiration = TimeSpan.FromMinutes(Math.Max(1, coreSettings.DefaultCacheDurationMinutes));
                        _cleanupInterval = TimeSpan.FromMinutes(Math.Max(1, coreSettings.CleanupIntervalMinutes));
                        _maxMemoryMB = 200; // 硬编码安全值
                        _minHitRate = 0.1; // 硬编码安全值
                    }
                    else
                    {
                        // 最终安全默认值
                        SetSafeDefaults();
                    }
                }
                
                _lastConfigUpdate = DateTime.Now;
            }
            catch (Exception ex)
            {
                Log.Warning($"[CacheService] Failed to refresh configuration: {ex.Message}, using safe defaults");
                SetSafeDefaults();
            }
        }

        /// <summary>
        /// 设置安全的默认配置 - 适应现代硬件
        /// </summary>
        private void SetSafeDefaults()
        {
            _maxEntries = 500;                          // 提高默认条目数
            _defaultExpiration = TimeSpan.FromMinutes(30); // 增加默认TTL
            _cleanupInterval = TimeSpan.FromMinutes(2);     // 适中的清理频率
            _maxMemoryMB = 200;                         // 提高内存限制
            _minHitRate = 0.1;
        }

        /// <summary>
        /// 获取Framework配置实例（如果可用）
        /// </summary>
        private dynamic GetFrameworkConfiguration()
        {
            try
            {
                // 使用反射获取Framework配置（避免直接依赖）
                var frameworkType = Type.GetType("RimAI.Framework.Configuration.RimAIConfiguration, RimAI.Framework");
                if (frameworkType != null)
                {
                    var instanceProperty = frameworkType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var frameworkInstance = instanceProperty?.GetValue(null);
                    if (frameworkInstance != null)
                    {
                        Log.Message("[CacheService] Successfully retrieved Framework configuration instance.");
                        return frameworkInstance;
                    }
                }
                Log.Warning("[CacheService] Framework configuration type found, but instance is null. Framework might not be fully initialized.");
            }
            catch (Exception ex)
            {
                Log.Message($"[CacheService] Framework configuration not available: {ex.Message}");
            }
            return null;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            // 定期刷新配置
            if (DateTime.Now - _lastConfigUpdate > TimeSpan.FromMinutes(5))
            {
                RefreshConfiguration();
            }

            Interlocked.Increment(ref _totalRequests);

            // 尝试从缓存获取
            if (_cache.TryGetValue(key, out var cachedEntry) && !cachedEntry.IsExpired)
            {
                cachedEntry.LastAccessed = DateTime.Now;
                cachedEntry.AccessCount++;
                Interlocked.Increment(ref _cacheHits);
                
                Log.Message($"[CacheService] Cache hit for key: {key}");
                return (T)cachedEntry.Value;
            }

            // 如果条目已过期，移除它
            if (cachedEntry?.IsExpired == true)
            {
                _cache.TryRemove(key, out _);
                Interlocked.Increment(ref _expirations);
            }

            Interlocked.Increment(ref _cacheMisses);

            // 缓存未命中，执行工厂方法
            Log.Message($"[CacheService] Cache miss for key: {key}, creating new entry");
            
            try
            {
                var value = await factory();
                
                // 判断是否应该缓存
                if (ShouldCacheRequest(key, value))
                {
                    var entry = new CacheItem
                    {
                        Key = key,
                        Value = value,
                        CreatedAt = DateTime.Now,
                        LastAccessed = DateTime.Now,
                        ExpiresAt = DateTime.Now + (expiration ?? _defaultExpiration),
                        AccessCount = 1
                    };

                    _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);

                    // 检查缓存大小并清理
                    if (_cache.Count > _maxEntries)
                    {
                        TriggerLRUCleanup();
                    }
                }

                return value;
            }
            catch (OperationCanceledException)
            {
                Log.Message($"[CacheService] Cache creation cancelled for key: {key}");
                throw;
            }
            catch (Exception ex)
            {
                Log.Error($"[CacheService] Failed to create cache entry for key {key}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 判断是否应该缓存请求
        /// </summary>
        private bool ShouldCacheRequest(string key, object value)
        {
            // 游戏启动时的优化：前100个tick不缓存
            if (Find.TickManager != null && Find.TickManager.TicksGame < 100)
            {
                Log.Message($"[CacheService] Skipping cache during game startup (tick {Find.TickManager.TicksGame})");
                return false;
            }

            // 内存压力检查
            var memoryUsageMB = EstimateMemoryUsage() / (1024 * 1024);
            if (memoryUsageMB > _maxMemoryMB * 0.9)
            {
                Log.Message($"[CacheService] Skipping cache due to memory pressure: {memoryUsageMB:F1}MB/{_maxMemoryMB}MB");
                return false;
            }

            // 缓存大小检查
            if (_cache.Count >= _maxEntries * 0.95)
            {
                Log.Message($"[CacheService] Skipping cache due to size limit: {_cache.Count}/{_maxEntries}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 估算内存使用量（字节）
        /// </summary>
        private long EstimateMemoryUsage()
        {
            long totalSize = 0;
            
            foreach (var entry in _cache.Values)
            {
                totalSize += 64; // 基础开销
                
                if (entry.Value is string str)
                {
                    totalSize += str.Length * 2 + 24;
                }
                else
                {
                    totalSize += 128; // 保守估算
                }
                
                totalSize += entry.Key?.Length * 2 + 32 ?? 32;
            }
            
            return totalSize;
        }

        /// <summary>
        /// 触发LRU清理
        /// </summary>
        private void TriggerLRUCleanup()
        {
            if (!Monitor.TryEnter(_cleanupLock))
                return;

            try
            {
                var entries = _cache.Values.OrderBy(e => e.LastAccessed).ToList();
                var removeCount = _cache.Count - _maxEntries + 50; // 多清理50个

                for (int i = 0; i < removeCount && i < entries.Count; i++)
                {
                    if (_cache.TryRemove(entries[i].Key, out _))
                    {
                        Interlocked.Increment(ref _evictions);
                    }
                }

                if (removeCount > 0)
                {
                    Log.Message($"[CacheService] LRU cleanup removed {removeCount} entries");
                }
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }

        public void Remove(string key)
        {
            if (string.IsNullOrEmpty(key)) return;

            if (_cache.TryRemove(key, out var entry))
            {
                Log.Message($"[CacheService] Removed cache entry for key: {key}");
            }
        }

        public void Clear()
        {
            var count = _cache.Count;
            _cache.Clear();
            
            // 重置统计信息
            Interlocked.Exchange(ref _totalRequests, 0);
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cacheMisses, 0);
            Interlocked.Exchange(ref _evictions, 0);
            Interlocked.Exchange(ref _expirations, 0);
            
            Log.Message($"[CacheService] Cleared {count} cache entries and reset statistics");
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    Interlocked.Increment(ref _expirations);
                    return false;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        private void CleanupExpiredEntries(object state)
        {
            if (!Monitor.TryEnter(_cleanupLock))
                return;

            try
            {
                var expiredKeys = _cache
                    .Where(kvp => kvp.Value.IsExpired)
                    .Select(kvp => kvp.Key)
                    .ToList();

                int cleanedCount = 0;
                foreach (var key in expiredKeys)
                {
                    if (_cache.TryRemove(key, out _))
                    {
                        cleanedCount++;
                        Interlocked.Increment(ref _expirations);
                    }
                }

                // 内存压力清理
                var memoryUsageMB = EstimateMemoryUsage() / (1024 * 1024);
                if (memoryUsageMB > _maxMemoryMB * 0.8)
                {
                    Log.Warning($"[CacheService] Memory usage ({memoryUsageMB:F1}MB) exceeds threshold, performing aggressive cleanup");
                    TriggerLRUCleanup();
                }

                // 低命中率清理
                if (_totalRequests > 50)
                {
                    var hitRate = _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0.0;
                    if (hitRate < _minHitRate)
                    {
                        Log.Warning($"[CacheService] Hit rate ({hitRate:P2}) below minimum ({_minHitRate:P2}), clearing cache");
                        Clear();
                    }
                }

                if (cleanedCount > 0)
                {
                    Log.Message($"[CacheService] Cleaned up {cleanedCount} expired entries");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CacheService] Error during cleanup: {ex.Message}");
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }

        /// <summary>
        /// 记录统计信息
        /// </summary>
        private void LogStatistics(object state)
        {
            try
            {
                var stats = GetStats();
                var memoryUsageMB = EstimateMemoryUsage() / (1024 * 1024);
                
                Log.Message($"[CacheService] Statistics - Entries: {stats.TotalEntries}/{_maxEntries}, Hit Rate: {stats.HitRate:P1}, Memory: ~{memoryUsageMB:F1}MB/{_maxMemoryMB}MB");
                
                // 健康状态警告
                if (stats.TotalEntries > _maxEntries * 0.8)
                {
                    Log.Warning($"[CacheService] Cache approaching size limit: {stats.TotalEntries}/{_maxEntries}");
                }
                
                if (memoryUsageMB > _maxMemoryMB * 0.8)
                {
                    Log.Warning($"[CacheService] Cache approaching memory limit: {memoryUsageMB:F1}MB/{_maxMemoryMB}MB");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CacheService] Error logging statistics: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStats GetStats()
        {
            var totalEntries = _cache.Count;
            var expiredEntries = 0;
            var totalAccessCount = 0L;

            foreach (var entry in _cache.Values)
            {
                if (entry.IsExpired)
                    expiredEntries++;
                
                totalAccessCount += entry.AccessCount;
            }

            var hitRate = _totalRequests > 0 ? (double)_cacheHits / _totalRequests : 0.0;

            return new CacheStats
            {
                TotalEntries = totalEntries,
                ExpiredEntries = expiredEntries,
                ActiveEntries = totalEntries - expiredEntries,
                TotalAccessCount = totalAccessCount,
                MaxEntries = _maxEntries,
                DefaultExpiration = _defaultExpiration,
                TotalRequests = _totalRequests,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRate = hitRate,
                Evictions = _evictions,
                Expirations = _expirations,
                MemoryUsageEstimate = EstimateMemoryUsage()
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                _cleanupTimer?.Dispose();
                _statsTimer?.Dispose();
                
                var stats = GetStats();
                Log.Message($"[CacheService] Disposed. Final stats - Entries: {stats.TotalEntries}, Hit Rate: {stats.HitRate:P1}, Total Requests: {stats.TotalRequests}");
                
                Clear();
            }
            catch (Exception ex)
            {
                Log.Error($"[CacheService] Error disposing: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 缓存条目
    /// </summary>
    internal class CacheItem
    {
        public string Key { get; set; }
        public object Value { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
        public DateTime ExpiresAt { get; set; }
        public long AccessCount { get; set; }

        public bool IsExpired => DateTime.Now > ExpiresAt;
    }

    /// <summary>
    /// 缓存统计信息 - 扩展版本，与Framework保持一致
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public int ActiveEntries { get; set; }
        public long TotalAccessCount { get; set; }
        public int MaxEntries { get; set; }
        public TimeSpan DefaultExpiration { get; set; }
        
        // Framework兼容的统计字段
        public long TotalRequests { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double HitRate { get; set; }
        public long Evictions { get; set; }
        public long Expirations { get; set; }
        public long MemoryUsageEstimate { get; set; }
    }
}
