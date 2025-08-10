using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Infrastructure;
using RimAI.Core.Modules.World;
using RimAI.Core.Modules.Persona;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Contracts.Eventing;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 简易聊天窗口：仅负责按参与者对（convKey）精确加载该对话的历史记录。
    /// 不做“交集回退”，避免误加载无关记录；若没有则创建一个新会话。
    /// </summary>
    public class MainTabWindow_Chat : Window
    {
        private readonly RimAI.Core.Services.IHistoryWriteService _history;
        private readonly IParticipantIdService _pidService;

        private string _convKeyInput = string.Empty;
        private string _selectedConversationId = string.Empty;
        private List<ConversationEntry> _entries = new List<ConversationEntry>();
        private Vector2 _scroll = Vector2.zero;
        private string _inputText = string.Empty;
        private bool _isSending = false;
        private string _status = string.Empty;
        private string _pendingPlayerMessage = null;
        private DateTime _pendingTimestamp;
        private string _streamAssistantBuffer = null;
        private readonly ConcurrentQueue<string> _streamQueue = new ConcurrentQueue<string>();
        private DateTime _streamLastDeltaAtUtc = DateTime.MinValue;
        private DateTime _streamStartedAtUtc = DateTime.MinValue;
        // 命令模式阶段性进度输出
        private readonly System.Collections.Concurrent.ConcurrentQueue<string> _progressQueue = new System.Collections.Concurrent.ConcurrentQueue<string>();
        private readonly System.Text.StringBuilder _progressSb = new System.Text.StringBuilder();
        private bool _progressSubscribed = false;
        private System.Action<IEvent> _progressHandler = null;

        private readonly string _modeTitle; // "闲聊" / "命令" 等
        private readonly IConfigurationService _config;
        private System.Threading.CancellationTokenSource _cts;

        private const float HeaderRowHeight = 56f; // 放大标题与头像区域
        private const float RowSpacing = 6f;
        private const float LeftMetaColWidth = 160f;

        public MainTabWindow_Chat(string convKey, string modeTitle)
        {
            _history = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
            _pidService = CoreServices.Locator.Get<IParticipantIdService>();
            _modeTitle = string.IsNullOrWhiteSpace(modeTitle) ? "聊天" : modeTitle.Trim();
            _config = CoreServices.Locator.Get<IConfigurationService>();

            forcePause = false;
            draggable = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;

            _convKeyInput = convKey ?? string.Empty;
            _ = EnsureExactLoadOrCreateAsync();
        }

        private Pawn TryResolvePawnFromConvKey()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_convKeyInput)) return null;
                var ids = _convKeyInput.Split('|');
                string pawnId = ids.FirstOrDefault(id => id.StartsWith("pawn:", StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(pawnId)) return null;
                var tail = pawnId.Substring("pawn:".Length);
                // 优先：当前地图的已生成殖民者
                foreach (var p in PawnsFinder.AllMaps_FreeColonistsSpawned)
                {
                    try
                    {
                        var uid = p?.GetUniqueLoadID();
                        var tid = p?.ThingID;
                        if (string.Equals(uid, tail, StringComparison.Ordinal) || string.Equals(tid, tail, StringComparison.Ordinal))
                            return p;
                    }
                    catch { /* ignore */ }
                }
                // 回退：当前地图所有已生成 Pawn
                var map = Find.CurrentMap;
                var allSpawned = map?.mapPawns?.AllPawnsSpawned;
                if (allSpawned != null)
                {
                    foreach (var p in allSpawned)
                    {
                        try
                        {
                            var uid = p?.GetUniqueLoadID();
                            var tid = p?.ThingID;
                            if (string.Equals(uid, tail, StringComparison.Ordinal) || string.Equals(tid, tail, StringComparison.Ordinal))
                                return p as Pawn;
                        }
                        catch { /* ignore */ }
                    }
                }
            }
            catch { /* ignore */ }
            return null;
        }

        public override Vector2 InitialSize => new Vector2(920f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            // 标题行：左侧头像 + 右侧加大字体标题
            DrawHeader(inRect, ref y);
            y += RowSpacing;

            // 历史列表 + 输入栏
            float inputHeight = 60f; // 略微降低输入区与按钮高度
            var historyRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - (inputHeight + RowSpacing));
            var inputRect = new Rect(inRect.x, historyRect.yMax + RowSpacing, inRect.width, inputHeight);
            FlushProgressLines();
            FlushStreamDeltas();
            DrawHistory(historyRect);
            DrawInputBar(inputRect);
        }

        private void FlushStreamDeltas()
        {
            if (_streamQueue == null) return;
            bool any = false;
            while (_streamQueue.TryDequeue(out var delta))
            {
                if (!string.IsNullOrEmpty(delta))
                {
                    _streamAssistantBuffer = (_streamAssistantBuffer ?? string.Empty) + delta;
                    _streamLastDeltaAtUtc = DateTime.UtcNow;
                    any = true;
                }
            }
            if (any)
            {
                // 轻推滚动条以促使重绘时更靠近底部
                _scroll.y = Mathf.Max(0, _scroll.y - 0.0001f);
            }
        }

        private void FlushProgressLines()
        {
            if (_progressQueue == null) return;
            while (_progressQueue.TryDequeue(out var line))
            {
                _progressSb.AppendLine(line);
            }
        }

        private void DrawHeader(Rect inRect, ref float y)
        {
            float avatarSize = 48f;
            var headerRect = new Rect(inRect.x, y, inRect.width, HeaderRowHeight);
            var avatarRect = new Rect(headerRect.x, headerRect.y + (HeaderRowHeight - avatarSize) / 2f, avatarSize, avatarSize);
            var titleRect = new Rect(avatarRect.xMax + 8f, headerRect.y, headerRect.width - avatarRect.width - 8f, headerRect.height);

            // 尝试解析会话中的 pawn 以显示头像
            var pawn = TryResolvePawnFromConvKey();
            if (pawn != null)
            {
                Widgets.ThingIcon(avatarRect, pawn);
            }
            else
            {
                Widgets.DrawBoxSolidWithOutline(avatarRect, new Color(0f, 0f, 0f, 0.15f), new Color(0f, 0f, 0f, 0.35f));
            }

            var oldFont = Text.Font; var oldAnchor = Text.Anchor;
            Text.Font = GameFont.Medium;

            // 左侧：标题（模式 + 参与者显示名）
            var leftRect = titleRect;
            Rect rightRect = default;
            string personaLabel = null;
            if (string.Equals(_modeTitle, "命令", StringComparison.Ordinal))
            {
                // 预留右侧区域显示人格
                float rightW = 240f;
                leftRect = new Rect(titleRect.x, titleRect.y, Mathf.Max(0f, titleRect.width - rightW), titleRect.height);
                rightRect = new Rect(leftRect.xMax, titleRect.y, rightW, titleRect.height);
                var personaName = GetBoundPersonaName();
                personaLabel = string.IsNullOrWhiteSpace(personaName) ? "未任命" : $"人格：{personaName}";
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(leftRect, GetHeaderText());

            // 右侧：人格名（命令模式）
            if (personaLabel != null)
            {
                var colorOld = GUI.color;
                GUI.color = string.Equals(personaLabel, "未任命", StringComparison.Ordinal) ? new Color(0.9f, 0.3f, 0.3f, 1f) : Color.white;
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(rightRect, personaLabel);
                GUI.color = colorOld;
            }

            Text.Font = oldFont; Text.Anchor = oldAnchor;
            y += HeaderRowHeight;
        }

        private string GetHeaderText()
        {
            if (string.IsNullOrWhiteSpace(_convKeyInput)) return _modeTitle;
            var names = _convKeyInput.Split('|').Select(id => _pidService.GetDisplayName(id));
            return $"{_modeTitle}：{string.Join(" ↔ ", names)}";
        }

        private string GetBoundPersonaName()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_convKeyInput)) return null;
                var pawnId = _convKeyInput.Split('|').FirstOrDefault(id => id.StartsWith("pawn:", StringComparison.Ordinal));
                if (string.IsNullOrWhiteSpace(pawnId)) return null;
                var binder = CoreServices.Locator.Get<IPersonaBindingService>();
                var binding = binder?.GetBinding(pawnId);
                var name = binding?.PersonaName;
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch { return null; }
        }

        private void DrawHistory(Rect rect)
        {
            var entries = _entries ?? new List<ConversationEntry>();

            // 估算高度（包含“待发送”行）
            float estimated = 0f;
            var cWidthEst = rect.width - 16f - LeftMetaColWidth - 8f;
            foreach (var e in entries)
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(e.Content ?? string.Empty, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(_pendingPlayerMessage, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            if (!string.IsNullOrEmpty(_streamAssistantBuffer))
            {
                estimated += Mathf.Max(22f, Text.CalcHeight(_streamAssistantBuffer, cWidthEst)) + 8f + RowSpacing + 4f;
            }
            var viewH = Math.Max(rect.height - 8f, estimated + 16f);
            var viewRect = new Rect(0, 0, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);

            float curY = 0f;
            var oldWrap = Text.WordWrap;
            Text.WordWrap = true;
            int rowIndex = 0;
            for (int i = 0; i < entries.Count; i++, rowIndex++)
            {
                var e = entries[i];
                float rowY = curY;

                // 计算行高
                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(e.Content ?? string.Empty, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;

                // 背景：仅玩家条目着色，AI 条目无底色
                bool isPlayer = e.SpeakerId?.StartsWith("player:") == true;
                if (isPlayer)
                {
                    var backRect = new Rect(0, rowY, viewRect.width, rowHeight);
                    Widgets.DrawBoxSolid(backRect, new Color(0.85f, 0.92f, 1f, 0.25f));
                }

                // 左列：时间+说话人（玩家显示别名）
                var alias = _config?.Current?.UI?.PlayerAlias ?? "总督";
                string speaker = (e.SpeakerId?.StartsWith("player:") ?? false) ? alias : (e.SpeakerId ?? "assistant");
                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{e.Timestamp:HH:mm:ss}] {speaker}");
                // 右列：内容
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, e.Content ?? string.Empty);

                curY = rowY + rowHeight;
            }

            // 附加渲染“待发送”的玩家消息（只显示，不入历史）
            if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
            {
                float rowY = curY;
                var alias = _config?.Current?.UI?.PlayerAlias ?? "总督";
                // 背景（按玩家配色）
                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(_pendingPlayerMessage, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;
                Widgets.DrawBoxSolid(new Rect(0, rowY, viewRect.width, rowHeight), new Color(0.85f, 0.92f, 1f, 0.25f));

                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{_pendingTimestamp:HH:mm:ss}] {alias}");
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, _pendingPlayerMessage);
                curY = rowY + rowHeight;
            }

            // 命令模式阶段性进度（只显示，不入历史）
            if (string.Equals(_modeTitle, "命令", StringComparison.Ordinal))
            {
                var progressText = _progressSb.ToString();
                if (!string.IsNullOrEmpty(progressText))
                {
                    float rowY = curY;
                    float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                    float contentHeight = Math.Max(22f, Text.CalcHeight(progressText, cWidth));
                    float rowHeight = contentHeight + RowSpacing + 8f;
                    Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), "[Progress] Orchestrator");
                    var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                    Widgets.Label(contentRect, progressText);
                    curY = rowY + rowHeight;
                }
            }

            // 渲染“AI 流式中”的临时内容（只显示，不入历史）
            if (!string.IsNullOrEmpty(_streamAssistantBuffer))
            {
                float rowY = curY;
                float cWidth = viewRect.width - LeftMetaColWidth - 8f;
                float contentHeight = Math.Max(22f, Text.CalcHeight(_streamAssistantBuffer, cWidth));
                float rowHeight = contentHeight + RowSpacing + 8f;
                // AI 行无底色
                var ts = (_streamStartedAtUtc == DateTime.MinValue ? DateTime.UtcNow : _streamStartedAtUtc);
                Widgets.Label(new Rect(0, rowY, LeftMetaColWidth, 22f), $"[{ts:HH:mm:ss}] assistant");
                var contentRect = new Rect(LeftMetaColWidth + 6f, rowY, cWidth, contentHeight);
                Widgets.Label(contentRect, _streamAssistantBuffer);
                curY = rowY + rowHeight;
            }
            Text.WordWrap = oldWrap;

            Widgets.EndScrollView();
        }

        private void DrawInputBar(Rect rect)
        {
            // 左侧输入框，右侧发送/取消按钮（不显示任何快捷键信息）
            float btnW = 120f;
            float inputH = rect.height;
            var inputRect = new Rect(rect.x, rect.y, rect.width - btnW - 8f - btnW - 6f, inputH);
            var sendBtnRect = new Rect(inputRect.xMax + 6f, rect.y, btnW, inputH);
            var cancelBtnRect = new Rect(sendBtnRect.xMax + 6f, rect.y, btnW, inputH);

            // 富文本框 + Enter发送（不阻止换行）
            GUI.SetNextControlName("RimAI.ChatInput");
            HandleEnterSend_DoNotSuppress();
            _inputText = Widgets.TextArea(inputRect, _inputText ?? string.Empty);
            // 占位提示（空且未输入时显示灰色文案）
            if (string.IsNullOrEmpty(_inputText))
            {
                string placeholder = "Enter发送；Shift+Enter换行";
                var oldCol = GUI.color; var oldFont = Text.Font; var oldAnchor = Text.Anchor;
                GUI.color = new Color(1f, 1f, 1f, 0.35f);
                Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.UpperLeft;
                var hintRect = new Rect(inputRect.x + 6f, inputRect.y + 4f, inputRect.width - 12f, inputRect.height - 8f);
                Widgets.Label(hintRect, placeholder);
                GUI.color = oldCol; Text.Font = oldFont; Text.Anchor = oldAnchor;
            }

            // 发送按钮（同输入框等高），不可用时显示“等待中”
            bool canSend = !_isSending && !string.IsNullOrWhiteSpace(_convKeyInput) && !string.IsNullOrWhiteSpace(_inputText);
            string sendLabel = _isSending ? "等待中" : "发送\nEnter发送";
            bool oldEnabled = GUI.enabled;
            GUI.enabled = canSend;
            if (Widgets.ButtonText(sendBtnRect, sendLabel))
            {
                _ = SendAsync();
            }
            GUI.enabled = oldEnabled;

                // 取消按钮（有在途请求时可点击）
            bool canCancel = _isSending && _cts != null;
            oldEnabled = GUI.enabled;
            GUI.enabled = canCancel;
            if (Widgets.ButtonText(cancelBtnRect, "取消"))
            {
                try { _cts?.Cancel(); } catch { }
                // 将待发送消息放回输入框
                if (!string.IsNullOrWhiteSpace(_pendingPlayerMessage))
                {
                    _inputText = _pendingPlayerMessage;
                    _pendingPlayerMessage = null;
                }
                    _streamAssistantBuffer = null;
                _isSending = false;
            }
            GUI.enabled = oldEnabled;
        }

        private void HandleEnterSend_DoNotSuppress()
        {
            var e = Event.current;
            if (e == null || e.type != EventType.KeyDown) return;
            if (!(e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) return;
            string focused = GUI.GetNameOfFocusedControl();
            bool focusOnInput = string.Equals(focused, "RimAI.ChatInput", StringComparison.Ordinal);
            if (!focusOnInput) return;
            // 允许 Shift+Enter 自然换行，这里不拦截
            if (e.shift) return;
            // 其他修饰键也放行（例如 Ctrl/Cmd），避免误伤
            if (e.alt || e.control || e.command) return;

            bool canSend = !_isSending && !string.IsNullOrWhiteSpace(_convKeyInput) && !string.IsNullOrWhiteSpace(_inputText);
            if (!canSend) return;

            _ = SendAsync();
            // 不调用 e.Use()，让 TextArea 自行决定是否插入换行（用户希望“算是小BUG，不管了”）
        }

        // 已按用户要求取消对回车的拦截逻辑


        private void SubscribeProgressOnce()
        {
            if (!string.Equals(_modeTitle, "命令", StringComparison.Ordinal)) return;
            if (_progressSubscribed) return;
            try
            {
                var bus = CoreServices.Locator.Get<IEventBus>();
                _progressHandler = (IEvent evt) =>
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
                        _progressQueue.Enqueue(line);
                        if (pPayload != null)
                        {
                            payload = pPayload.GetValue(evt) as string;
                            int max = System.Math.Max(0, pc?.PayloadPreviewChars ?? 200);
                            if (!string.IsNullOrEmpty(payload))
                            {
                                if (payload.Length > max) payload = payload.Substring(0, max) + "…";
                                _progressQueue.Enqueue("  payload: " + payload);
                            }
                        }
                    }
                    catch
                    {
                        _progressQueue.Enqueue("[Progress] " + evt.Describe());
                    }
                };
                bus?.Subscribe(_progressHandler);
                _progressSubscribed = true;
            }
            catch { /* ignore */ }
        }

        private void UnsubscribeProgress()
        {
            if (!_progressSubscribed) return;
            try
            {
                var bus = CoreServices.Locator.Get<IEventBus>();
                if (_progressHandler != null)
                {
                    bus?.Unsubscribe(_progressHandler);
                }
            }
            catch { }
            finally
            {
                _progressSubscribed = false;
                _progressHandler = null;
            }
        }

        private async Task SendAsync()
        {
            if (_isSending) return;
            _isSending = true;
            _status = "处理中…";
            _cts?.Dispose();
            _cts = new System.Threading.CancellationTokenSource();
            var ct = _cts.Token;
            try
            {
                // 启动发送：把输入行作为“待发送”显示到列表底部
                _pendingPlayerMessage = _inputText ?? string.Empty;
                _pendingTimestamp = DateTime.UtcNow;
                _inputText = string.Empty;

                var parts = (_convKeyInput ?? string.Empty).Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if (parts.Count == 0)
                {
                    parts = new List<string> { _pidService.GetPlayerId(), "pawn:UNKNOWN" };
                    _convKeyInput = CanonicalizeConvKey(string.Join("|", parts));
                }

                var svc = CoreServices.Locator.Get<IPersonaConversationService>();
                _streamAssistantBuffer = string.Empty;
                _streamStartedAtUtc = DateTime.UtcNow;
                _streamLastDeltaAtUtc = _streamStartedAtUtc;
                // 在后台线程消费流并投递增量，窗口帧循环中 Flush 并绘制
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    string final = string.Empty;
                    try
                    {
                        if (string.Equals(_modeTitle, "命令", StringComparison.Ordinal))
                        {
                            var opts = new PersonaCommandOptions { Stream = true, WriteHistory = true };
                            // 新一轮命令前清空进度与流式缓冲
                            try { _progressSb.Clear(); } catch { }
                            _streamAssistantBuffer = string.Empty;
                            // 订阅进度事件（仿 Debug 页）
                            SubscribeProgressOnce();
                            await foreach (var chunk in svc.CommandAsync(parts, null, _pendingPlayerMessage, opts, ct))
                            {
                                if (ct.IsCancellationRequested) break;
                                if (chunk.IsSuccess)
                                {
                                    var delta = chunk.Value?.ContentDelta ?? string.Empty;
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        _streamQueue.Enqueue(delta);
                                        final += delta;
                                        _streamLastDeltaAtUtc = DateTime.UtcNow;
                                    }
                                }
                                else
                                {
                                    // 将错误信息也输出到进度区域，避免静默失败
                                    var err = chunk.Error ?? "未知错误";
                                    _progressQueue.Enqueue("[Error] " + err);
                                }
                                // FinishReason 报告时立即跳出
                                if (!string.IsNullOrEmpty(chunk.Value?.FinishReason)) break;
                                // 静默 watchdog：120s 无新 delta 则取消（命令模式可能较慢）
                                if ((DateTime.UtcNow - _streamLastDeltaAtUtc).TotalSeconds > 120)
                                {
                                    try { _cts?.Cancel(); } catch { }
                                    break;
                                }
                            }
                            if (!ct.IsCancellationRequested)
                            {
                                try
                                {
                                    var list = await _history.FindByConvKeyAsync(_convKeyInput);
                                    _selectedConversationId = list?.LastOrDefault() ?? _selectedConversationId;
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            var opts = new PersonaChatOptions { Stream = true, WriteHistory = false };
                            await foreach (var chunk in svc.ChatAsync(parts, null, _pendingPlayerMessage, opts, ct))
                            {
                                if (ct.IsCancellationRequested) break;
                                if (chunk.IsSuccess)
                                {
                                    var delta = chunk.Value?.ContentDelta ?? string.Empty;
                                    if (!string.IsNullOrEmpty(delta))
                                    {
                                        _streamQueue.Enqueue(delta);
                                        final += delta;
                                        _streamLastDeltaAtUtc = DateTime.UtcNow;
                                    }
                                }
                                if (!string.IsNullOrEmpty(chunk.Value?.FinishReason)) break;
                                if ((DateTime.UtcNow - _streamLastDeltaAtUtc).TotalSeconds > 60)
                                {
                                    try { _cts?.Cancel(); } catch { }
                                    break;
                                }
                            }
                            if (!ct.IsCancellationRequested)
                            {
                                try
                                {
                                    if (string.IsNullOrWhiteSpace(_selectedConversationId))
                                    {
                                        _selectedConversationId = _history.CreateConversation(parts);
                                    }
                                    var now = DateTime.UtcNow;
                                    var playerId = parts.FirstOrDefault(id => id.StartsWith("player:")) ?? _pidService.GetPlayerId();
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry(playerId, _pendingPlayerMessage ?? string.Empty, now));
                                    await _history.AppendEntryAsync(_selectedConversationId, new ConversationEntry("assistant", final ?? string.Empty, now.AddMilliseconds(1)));
                                }
                                catch { }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _streamQueue.Enqueue($"\n[Error] {ex.Message}\n");
                    }
                    finally
                    {
                        try { if (!ct.IsCancellationRequested) await ReloadEntriesAsync(); } catch { }
                        // 退订进度事件
                        UnsubscribeProgress();
                        _streamAssistantBuffer = null;
                        _pendingPlayerMessage = null;
                        _isSending = false;
                        try { _cts?.Dispose(); } catch { }
                        _cts = null;
                    }
                });
                return; // 后台任务已启动，UI 帧继续
            }
            catch (Exception ex)
            {
                _status = "失败: " + ex.Message;
                _isSending = false;
                _pendingPlayerMessage = null;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
            }
        }

        // 移除 OnAcceptKeyPressed 的快捷键处理，集中在 HandleHotkeys 里，避免重复与冲突

        private async Task EnsureExactLoadOrCreateAsync()
        {
            try
            {
                var canon = CanonicalizeConvKey(_convKeyInput);
                _convKeyInput = canon;

                // 仅精确匹配，不做“参与者交集回退”
                var list = await _history.FindByConvKeyAsync(canon);
                var ids = list?.ToList() ?? new List<string>();
                if (ids.Count == 0)
                {
                    // 不存在则创建新会话
                    var parts = canon.Split('|').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (parts.Count == 0)
                    {
                        // 若 convKey 为空，尝试用 player 与 demo pawn 占位，避免空崩溃
                        parts = new List<string> { _pidService.GetPlayerId(), "pawn:UNKNOWN" };
                        canon = CanonicalizeConvKey(string.Join("|", parts));
                        _convKeyInput = canon;
                    }
                    _selectedConversationId = _history.CreateConversation(parts);
                }
                else
                {
                    _selectedConversationId = ids.Last(); // 选择最新
                }
                await ReloadEntriesAsync();
            }
            catch (Exception ex)
            {
                Messages.Message("加载聊天失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }

        private async Task ReloadEntriesAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedConversationId)) { _entries = new List<ConversationEntry>(); return; }
            try
            {
                var rec = await _history.GetConversationAsync(_selectedConversationId);
                _entries = rec?.Entries?.ToList() ?? new List<ConversationEntry>();
            }
            catch
            {
                _entries = new List<ConversationEntry>();
            }
        }

        private static string CanonicalizeConvKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;
            var ids = key.Split('|').Select(s => s?.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join("|", ids.OrderBy(x => x, StringComparer.Ordinal));
        }
    }
}


