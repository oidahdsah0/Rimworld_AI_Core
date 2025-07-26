using System;
using System.Collections.Concurrent;    // 引入线程安全字典
using RimAI.Core.Contracts.Services;
using Verse; // 引入Verse，打印Log信息

namespace RimAI.Core.Architecture.Caching
{
    // 实现之前定义的ICacheService泛型接口
    public class CacheService<TKey, TValue> : ICacheService<TKey, TValue> where TValue : class
    {
        // 内部存储的核心： 一个线程安全的ConcurrentDictionary
        private readonly ConcurrentDictionary<TKey, CacheItem> _cache = new ConcurrentDictionary<TKey, CacheItem>();

        public bool TryGetValue(TKey key, out TValue value) // Corrected return type
        {
            value = null; // 默认值

            // 尝试从字典中获取 CacheItem
            if (_cache.TryGetValue(key, out var cacheItem))
            {
                // 检查这个条目是否已经过期
                if (cacheItem.Expiration > DateTime.UtcNow)
                {
                    // 未过期，返回值并Log成功
                    value = cacheItem.Value;
                    Log.Message($"[RimAI.Core.CacheService] Cache hit for key: {key}");
                    return true;
                }
                // 过期，从字典中删除这个条目
                _cache.TryRemove(key, out _);
                Log.Message($"[RimAI.Core.CacheService] CACHE EXPIRED. KEY: {key}");
            }
            // 缓存未命中或已过期
            Log.Message($"[RimAI.Core.CacheService] CACHE MISS. KEY: {key}");
            return false;
        }

        public void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow) // Corrected method and parameter name
        {
            // 创建一个新的 CacheItem
            var newItem = new CacheItem
            {
                Value = value,
                Expiration = DateTime.UtcNow.Add(absoluteExpirationRelativeToNow)
            };
            // 使用 AddOrUpdate 方法来原子性地插入或更新条目
            _cache.AddOrUpdate(key, newItem, (k, existingItem) => newItem);
            Log.Message($"[RimAI.Core.CacheService] CACHE SET. KEY: {key}"); // Corrected log message
        }

        // 这是一个嵌套的私有类，只在 CacheService 内部使用
        private class CacheItem
        {
            public TValue Value { get; set; }
            public DateTime Expiration { get; set; }
        }
    }
}