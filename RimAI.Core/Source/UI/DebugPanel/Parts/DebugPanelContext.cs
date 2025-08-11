using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimAI.Core.Infrastructure;
using RimAI.Framework.Contracts;

namespace RimAI.Core.UI.DebugPanel.Parts
{
    /// <summary>
    /// 调试面板运行时上下文，封装常用能力并避免按钮实现直接依赖入口窗体。
    /// </summary>
    public sealed class DebugPanelContext
    {
        public Action<string> AppendOutput { get; }

        /// <summary>
        /// 处理流式输出的统一帮助方法。
        /// </summary>
        public Func<string, IAsyncEnumerable<Result<UnifiedChatChunk>>, Task> HandleStreamingOutputAsync { get; }

        /// <summary>
        /// 直接追加原始文本（不自动换行、不加时间戳），用于流式增量。
        /// </summary>
        public Action<string> EnqueueRaw { get; }

        /// <summary>
        /// 清空输出窗口（包括待写入队列与滚动内容）。
        /// </summary>
        public Action ClearOutput { get; }

        public DebugPanelContext(
            Action<string> appendOutput,
            Func<string, IAsyncEnumerable<Result<UnifiedChatChunk>>, Task> handleStreamingOutputAsync,
            Action<string> enqueueRaw,
            Action clearOutput)
        {
            AppendOutput = appendOutput ?? throw new ArgumentNullException(nameof(appendOutput));
            HandleStreamingOutputAsync = handleStreamingOutputAsync ?? throw new ArgumentNullException(nameof(handleStreamingOutputAsync));
            EnqueueRaw = enqueueRaw ?? throw new ArgumentNullException(nameof(enqueueRaw));
            ClearOutput = clearOutput ?? throw new ArgumentNullException(nameof(clearOutput));
        }

        /// <summary>
        /// 通过服务定位器获取依赖。
        /// </summary>
        public T Get<T>() where T : class
        {
            return CoreServices.Locator.Get<T>();
        }

        /// <summary>
        /// 计算短哈希，用于构造对话 ID 或调试标识。
        /// </summary>
        public string ComputeShortHash(string input)
        {
            try
            {
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(input ?? string.Empty);
                    var hash = sha1.ComputeHash(bytes);
                    var sb = new System.Text.StringBuilder(20);
                    for (int i = 0; i < System.Math.Min(hash.Length, 10); i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "0000000000"; }
        }
    }
}


