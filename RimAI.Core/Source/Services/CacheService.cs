using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services
{
    /// <summary>
    /// 缓存服务实现 - 提供智能缓存功能
    /// </summary>
    public class CacheService : ICacheService
    {
        private static CacheService _instance;
        public static CacheService Instance => _instance ??= new CacheService();

        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly object _cleanupLock = new object();

        // 默认过期时间
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(5);
        private readonly int _maxEntries = 1000;

        private CacheService()
        {
            _cache = new ConcurrentDictionary<string, CacheEntry>();
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            // 尝试从缓存获取
            if (_cache.TryGetValue(key, out var cachedEntry) && !cachedEntry.IsExpired)
            {
                cachedEntry.LastAccessed = DateTime.Now;
                cachedEntry.AccessCount++;
                
                Log.Message($"[CacheService] Cache hit for key: {key}");
                return (T)cachedEntry.Value;
            }

            // 缓存未命中，执行工厂方法
            Log.Message($"[CacheService] Cache miss for key: {key}, creating new entry");
            
            try
            {
                var value = await factory();
                var entry = new CacheEntry
                {
                    Key = key,
                    Value = value,
                    CreatedAt = DateTime.Now,
                    LastAccessed = DateTime.Now,
                    ExpiresAt = DateTime.Now + (expiration ?? _defaultExpiration),
                    AccessCount = 1
                };

                _cache.AddOrUpdate(key, entry, (k, oldEntry) => entry);

                // 定期清理过期项
                CleanupExpiredEntries();

                return value;
            }
            catch (Exception ex)
            {
                Log.Error($"[CacheService] Failed to create cache entry for key {key}: {ex.Message}");
                throw;
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
            Log.Message($"[CacheService] Cleared {count} cache entries");
        }

        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.IsExpired)
                {
                    _cache.TryRemove(key, out _);
                    return false;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        private void CleanupExpiredEntries()
        {
            // 避免过于频繁的清理
            if (!Monitor.TryEnter(_cleanupLock))
                return;

            try
            {
                var expiredKeys = new List<string>();
                var now = DateTime.Now;

                foreach (var kvp in _cache)
                {
                    if (kvp.Value.IsExpired)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _cache.TryRemove(key, out _);
                }

                if (expiredKeys.Count > 0)
                {
                    Log.Message($"[CacheService] Cleaned up {expiredKeys.Count} expired entries");
                }

                // 如果缓存过大，清理最少使用的项
                if (_cache.Count > _maxEntries)
                {
                    CleanupLeastRecentlyUsed();
                }
            }
            finally
            {
                Monitor.Exit(_cleanupLock);
            }
        }

        /// <summary>
        /// 清理最少使用的缓存项
        /// </summary>
        private void CleanupLeastRecentlyUsed()
        {
            var entries = new List<CacheEntry>();
            
            foreach (var kvp in _cache)
            {
                entries.Add(kvp.Value);
            }

            // 按访问时间排序，移除最旧的项
            entries.Sort((a, b) => a.LastAccessed.CompareTo(b.LastAccessed));
            
            var removeCount = _cache.Count - _maxEntries + 100; // 多清理100个，避免频繁清理
            
            for (int i = 0; i < removeCount && i < entries.Count; i++)
            {
                _cache.TryRemove(entries[i].Key, out _);
            }

            Log.Message($"[CacheService] Cleaned up {removeCount} least recently used entries");
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStats GetStats()
        {
            var now = DateTime.Now;
            var totalEntries = _cache.Count;
            var expiredEntries = 0;
            var totalAccessCount = 0L;

            foreach (var entry in _cache.Values)
            {
                if (entry.IsExpired)
                    expiredEntries++;
                
                totalAccessCount += entry.AccessCount;
            }

            return new CacheStats
            {
                TotalEntries = totalEntries,
                ExpiredEntries = expiredEntries,
                ActiveEntries = totalEntries - expiredEntries,
                TotalAccessCount = totalAccessCount,
                MaxEntries = _maxEntries,
                DefaultExpiration = _defaultExpiration
            };
        }
    }

    /// <summary>
    /// 缓存条目
    /// </summary>
    internal class CacheEntry
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
    /// 缓存统计信息
    /// </summary>
    public class CacheStats
    {
        public int TotalEntries { get; set; }
        public int ExpiredEntries { get; set; }
        public int ActiveEntries { get; set; }
        public long TotalAccessCount { get; set; }
        public int MaxEntries { get; set; }
        public TimeSpan DefaultExpiration { get; set; }
    }
}
