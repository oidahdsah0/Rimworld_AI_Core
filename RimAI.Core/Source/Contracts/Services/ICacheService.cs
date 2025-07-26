using System;

namespace RimAI.Core.Contracts.Services
{
    // TValue : class 约束 TValue 必须是一个引用类型
    public interface ICacheService<TKey, TValue> where TValue : class
    {
        // 尝试从缓存中获取值
        bool TryGetValue(TKey key, out TValue value);

        // 将一个值添加到缓存中，并设置其绝对过期时间
        void Set(TKey key, TValue value, TimeSpan absoluteExpirationRelativeToNow);
    }
}