using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace RimAI.Core.Infrastructure.Extensions
{
    /// <summary>
    /// 针对 IAsyncEnumerable 的通用扩展工具。
    /// 提供错误包装，确保异常以单一“失败项”的形式返回，同时保证枚举器被释放。
    /// </summary>
    internal static class AsyncEnumerableExtensions
    {
        /// <summary>
        /// 包装异步流，将枚举过程中的异常转换为一个由 <paramref name="onError"/> 生成的单条结果，然后终止流。
        /// - 使用 await using 确保枚举器释放；
        /// - 使用 [EnumeratorCancellation] 传递取消令牌；
        /// - 一旦发生异常，仅返回一次错误，并结束迭代。
        /// </summary>
        public static async IAsyncEnumerable<T> WrapErrors<T>(
            this IAsyncEnumerable<T> source,
            Func<Exception, T> onError,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await using var enumerator = source.GetAsyncEnumerator(cancellationToken);
            Exception captured = null;
            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = await enumerator.MoveNextAsync();
                }
                catch (Exception ex)
                {
                    captured = ex;
                    hasNext = false;
                }

                if (captured != null)
                    break;

                if (!hasNext)
                    yield break;

                yield return enumerator.Current;
            }

            // 在 try/catch 之外统一返回错误项
            yield return onError(captured);
        }
    }
}


