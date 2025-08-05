using System;

namespace RimAI.Core.Infrastructure.Cache
{
    public interface ICacheService
    {
        bool TryGet<T>(string key, out T value);
        void Set<T>(string key, T value, TimeSpan expiration);
        int HitCount { get; }
    }
}