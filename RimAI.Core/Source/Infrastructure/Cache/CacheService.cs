using System;
using System.Collections.Concurrent;

namespace RimAI.Core.Infrastructure.Cache
{
    internal sealed class CacheService : ICacheService
    {
        private class CacheEntry
        {
            public object Value { get; init; }
            public DateTime ExpirationUtc { get; init; }
        }

        private readonly ConcurrentDictionary<string, CacheEntry> _dict = new();
        private int _hit;
        public int HitCount => _hit;

        public bool TryGet<T>(string key, out T value)
        {
            value = default;
            if (_dict.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow <= entry.ExpirationUtc)
                {
                    _hit++;
                    value = (T)entry.Value;
                    return true;
                }
                // 过期自动剔除
                _dict.TryRemove(key, out _);
            }
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            var entry = new CacheEntry { Value = value, ExpirationUtc = DateTime.UtcNow.Add(expiration) };
            _dict[key] = entry;
        }
    }
}