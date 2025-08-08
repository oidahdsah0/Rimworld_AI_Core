using System;
using System.Collections.Concurrent;
using System.Threading;

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
                    // 类型安全检查，避免错误的强制转换导致异常
                    if (entry.Value is T typed)
                    {
                        Interlocked.Increment(ref _hit);
                        value = typed;
                        return true;
                    }
                    return false;
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