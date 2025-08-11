using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using RimAI.Core.Contracts.Models;
using RimAI.Core.Contracts.Eventing;
using UnityEngine;
using Verse;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// MainTabWindow_Chat 的状态与常量定义。
    /// </summary>
    public partial class MainTabWindow_Chat
    {
        // Services
        private readonly RimAI.Core.Services.IHistoryWriteService _history;
        private readonly RimAI.Core.Modules.World.IParticipantIdService _pidService;
        private readonly RimAI.Core.Infrastructure.Configuration.IConfigurationService _config;

        // UI/State
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
        private readonly ConcurrentQueue<string> _progressQueue = new ConcurrentQueue<string>();
        private readonly System.Text.StringBuilder _progressSb = new System.Text.StringBuilder();
        private bool _progressSubscribed = false;
        private Action<IEvent> _progressHandler = null;

        private readonly string _modeTitle; // "闲聊" / "命令" 等
        private System.Threading.CancellationTokenSource _cts;

        // Layout constants
        private const float HeaderRowHeight = 56f; // 放大标题与头像区域
        private const float RowSpacing = 6f;
        private const float LeftMetaColWidth = 160f;
        private const float SubHeaderRowHeight = 32f; // 标题下操作行：左人格状态，右按钮
        private const float IndicatorsRowHeight = 18f; // 指示灯行高度

        // 缓冲上限（仅限 UI 预览，不影响写入历史的完整文本）
        private const int MaxProgressChars = 8000;        // 进度预览最大字符数
        private const int MaxStreamPreviewChars = 12000;   // 流式预览最大字符数

        // Pawn 解析缓存，避免每帧扫描
        private Pawn _cachedPawn;
        private string _cachedPawnConvKey;

        // 指示灯控制
        private DateTime _indicatorRedUntilUtc = DateTime.MinValue;   // 有新 chunk 时短暂点亮红灯
        private DateTime _indicatorGreenUntilUtc = DateTime.MinValue; // 完成时点亮绿灯一段时间
        private DateTime _lastIndicatorSoundAtUtc = DateTime.MinValue; // 读取磁盘音效节流
        private const int MinIndicatorSoundIntervalMs = 120; // 指示灯音效最小间隔

        private enum SendMode
        {
            Chat,
            Command
        }
    }
}


