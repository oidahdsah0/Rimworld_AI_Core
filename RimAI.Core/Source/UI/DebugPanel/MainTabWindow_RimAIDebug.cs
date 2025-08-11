using UnityEngine;
using RimWorld;
using Verse;
using RimAI.Core.Infrastructure;
using RimAI.Core.Contracts.Eventing;
using RimAI.Framework.Contracts;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using RimAI.Core.UI.DebugPanel.Parts;

namespace RimAI.Core.UI.DebugPanel
{
    /// <summary>
    /// 开发者调试面板入口/总组装。
    /// 按钮逻辑已拆分至 Parts，每个按钮一个独立测试。
    /// </summary>
    public class MainTabWindow_RimAIDebug : MainTabWindow
    {
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 160f;
        private const float Padding = 10f;
        private const float OutputAreaHeight = 380f;
        private const int MaxFlushPerFrame = 2000; // 防止一次 flush 过多导致卡顿
        private const int MaxOutputChars = 200_000; // 限制输出长度，避免内存膨胀

        private readonly System.Text.StringBuilder _outputSb = new System.Text.StringBuilder();
        private Vector2 _outputScroll = Vector2.zero;
        private readonly ConcurrentQueue<string> _pendingChunks = new();
        private bool _subscribed;
        private System.Action<RimAI.Core.Contracts.Eventing.IEvent> _progressHandler;

        private readonly List<IDebugPanelButton> _firstRowButtons;
        private readonly List<IDebugPanelButton> _secondRowButtons;
        private const int ButtonsPerRow = 10;

        public MainTabWindow_RimAIDebug()
        {
            var all = DiscoverButtons();
            // 固定把 Clear Output 放在最前
            all.Insert(0, new Parts.ClearOutputButton());
            _firstRowButtons = all.Take(ButtonsPerRow).ToList();
            _secondRowButtons = all.Skip(ButtonsPerRow).Take(ButtonsPerRow).ToList();
        }

        private static List<IDebugPanelButton> DiscoverButtons()
        {
            var list = new List<IDebugPanelButton>();
            try
            {
                // 仅扫描当前程序集中的 Parts 命名空间类型
                var asm = typeof(MainTabWindow_RimAIDebug).Assembly;
                var types = asm.GetTypes();
                foreach (var t in types)
                {
                    try
                    {
                        if (t == null || t.IsAbstract || t.IsInterface) continue;
                        if (!typeof(IDebugPanelButton).IsAssignableFrom(t)) continue;
                        if (t == typeof(Parts.ClearOutputButton)) continue; // 稍后手动插入
                        // 通过无参构造创建
                        var inst = System.Activator.CreateInstance(t) as IDebugPanelButton;
                        if (inst == null) continue;
                        // 排除不应露出的内部测试类（约定：Label 为空）
                        if (string.IsNullOrWhiteSpace(inst.Label)) continue;
                        list.Add(inst);
                    }
                    catch { /* ignore type */ }
                }
            }
            catch { /* ignore asm scan */ }

            // 基于标签名稳定排序，避免每次加载顺序变化
            list = list.OrderBy(x => x.Label, System.StringComparer.Ordinal).ToList();
            return list;
        }

        private void AppendOutput(string msg)
        {
            _pendingChunks.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {msg}\n");
        }
        
        /// <summary>
        /// 处理流式输出的辅助函数
        /// </summary>
        private async Task HandleStreamingOutputAsync(string streamName, IAsyncEnumerable<Result<UnifiedChatChunk>> stream)
        {
            try
            {
                _pendingChunks.Enqueue($"[{System.DateTime.Now:HH:mm:ss}] {streamName}: "); // 开始流式输出，不加换行符

                if (stream == null)
                {
                    _pendingChunks.Enqueue("[Error] stream is null\n");
                    return;
                }

                string finalFinishReason = null;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                await foreach (var chunk in stream)
                {
                    if (chunk.IsSuccess)
                    {
                        var delta = chunk.Value?.ContentDelta;
                        if (!string.IsNullOrEmpty(delta))
                        {
                            _pendingChunks.Enqueue(delta);
                        }
                        if (!string.IsNullOrEmpty(chunk.Value?.FinishReason))
                        {
                            finalFinishReason = chunk.Value.FinishReason;
                        }
                    }
                    else
                    {
                        _pendingChunks.Enqueue($"[Error] {chunk.Error}");
                    }
                }
                sw.Stop();

                _pendingChunks.Enqueue("\n"); // 流式传输结束后换行

                if (finalFinishReason != null)
                {
                    _pendingChunks.Enqueue($"[FINISH: {finalFinishReason}] (耗时: {sw.Elapsed.TotalSeconds:F2} s)\n");
                }
            }
            catch (System.Exception ex)
            {
                _pendingChunks.Enqueue($"{streamName} failed: {ex.Message}\n");
            }
        }


        private const float ExtraWidth = 30f;
        public override Vector2 InitialSize => new(
            ButtonWidth * Mathf.Max(_firstRowButtons.Count, _secondRowButtons.Count) + Padding * (Mathf.Max(_firstRowButtons.Count, _secondRowButtons.Count) + 1) + ExtraWidth,
            (ButtonHeight + Padding * 3 + OutputAreaHeight) * 1.2f);

        public override void DoWindowContents(Rect inRect)
        {
            // 将后台线程生成的增量 flush 到输出
            int flushed = 0;
            while (flushed < MaxFlushPerFrame && _pendingChunks.TryDequeue(out var part))
            {
                _outputSb.Append(part);
                flushed++;
            }
            // 裁剪输出，避免无限增长
            if (_outputSb.Length > MaxOutputChars)
            {
                int remove = _outputSb.Length - MaxOutputChars;
                if (remove > 0) _outputSb.Remove(0, remove);
            }

            // 订阅编排进度事件（一次性）
            if (!_subscribed)
            {
                _subscribed = true;
                var bus = CoreServices.Locator.Get<IEventBus>();
                _progressHandler = (evt =>
                {
                    try
                    {
                        var cfg = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();
                        var pc = cfg?.Current?.Orchestration?.Progress;
                        string template = pc?.DefaultTemplate ?? "[{Source}] {Stage}: {Message}";
                        string source = null, stage = null, message = evt.Describe();
                        string payload = null;
                        var t = evt.GetType();
                        var pStage = t.GetProperty("Stage");
                        var pSource = t.GetProperty("Source");
                        var pMessage = t.GetProperty("Message");
                        var pPayload = t.GetProperty("PayloadJson");
                        if (pStage != null) stage = pStage.GetValue(evt) as string;
                        if (pSource != null) source = pSource.GetValue(evt) as string;
                        if (pMessage != null) message = pMessage.GetValue(evt) as string ?? message;
                        if (pc?.StageTemplates != null && stage != null && pc.StageTemplates.TryGetValue(stage, out var st))
                            template = st;
                        string line = template
                            .Replace("{Source}", source ?? string.Empty)
                            .Replace("{Stage}", stage ?? string.Empty)
                            .Replace("{Message}", message ?? string.Empty);
                        _pendingChunks.Enqueue($"[Progress] {line}\n");
                        if (pPayload != null)
                        {
                            payload = pPayload.GetValue(evt) as string;
                            int max = System.Math.Max(0, pc?.PayloadPreviewChars ?? 200);
                            if (!string.IsNullOrEmpty(payload))
                            {
                                if (payload.Length > max) payload = payload.Substring(0, max) + "…";
                                _pendingChunks.Enqueue($"  payload: {payload}\n");
                            }
                        }
                    }
                    catch
                    {
                        _pendingChunks.Enqueue($"[Progress] {evt.Describe()}\n");
                    }
                });
                bus?.Subscribe(_progressHandler);
            }

            // 运行时上下文
            var ctx = new DebugPanelContext(
                AppendOutput,
                HandleStreamingOutputAsync,
                s => _pendingChunks.Enqueue(s),
                () => { _outputSb.Length = 0; while (_pendingChunks.TryDequeue(out _)) { } _outputScroll = Vector2.zero; }
            );

            // 1. 顶部横向按钮 -----------------------------
            float curX = inRect.x + Padding;
            float curY = inRect.y + Padding;

            // 本地函数用于创建按钮并推进 X 坐标
            bool Button(string label)
            {
                var rect = new Rect(curX, curY, ButtonWidth, ButtonHeight);
                bool clicked = Widgets.ButtonText(rect, label);
                curX += ButtonWidth + Padding;
                return clicked;
            }

            // 渲染第一行按钮
            foreach (var b in _firstRowButtons)
            {
                if (Button(b.Label))
                {
                    try { b.Execute(ctx); } catch (System.Exception ex) { AppendOutput($"{b.Label} failed: {ex.Message}"); }
                }
            }

            // 第二行按钮 -----------------------------
            curX = inRect.x + Padding;
            curY += ButtonHeight + Padding;
            foreach (var b in _secondRowButtons)
            {
                if (Button(b.Label))
                {
                    try { b.Execute(ctx); } catch (System.Exception ex) { AppendOutput($"{b.Label} failed: {ex.Message}"); }
                }
            }

            // 2. 输出窗口 -----------------------------
            float outputY = curY + ButtonHeight + Padding;
            var outputRect = new Rect(inRect.x + Padding, outputY, inRect.width - 2 * Padding, OutputAreaHeight);

            var viewWidth = outputRect.width - 16f; // 考虑滚动条宽度
            var viewHeight = Mathf.Max(OutputAreaHeight, Text.CalcHeight(_outputSb.ToString(), viewWidth));
            var viewRect = new Rect(0, 0, viewWidth, viewHeight);

            Widgets.BeginScrollView(outputRect, ref _outputScroll, viewRect);
            Widgets.Label(viewRect, _outputSb.ToString());
            Widgets.EndScrollView();
        }

        public override void PostClose()
        {
            base.PostClose();
            try
            {
                if (_subscribed && _progressHandler != null)
                {
                    var bus = CoreServices.Locator.Get<IEventBus>();
                    bus?.Unsubscribe<IEvent>(_progressHandler);
                    _subscribed = false;
                    _progressHandler = null;
                }
            }
            catch { /* ignore */ }
        }

        // 注意：HandleStreamingOutputAsync 方法已在上方定义，供上下文委托复用
    }
}
