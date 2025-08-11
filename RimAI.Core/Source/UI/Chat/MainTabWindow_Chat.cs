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
using RimAI.Core.Contracts.Services;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI.Chat
{
    /// <summary>
    /// 简易聊天窗口：仅负责按参与者对（convKey）精确加载该对话的历史记录。
    /// 不做“交集回退”，避免误加载无关记录；若没有则创建一个新会话。
    /// 入口/总组装：仅保留构造、尺寸与绘制流程调度，其余拆分到 Parts/ 下。
    /// </summary>
    public partial class MainTabWindow_Chat : Window
    {
        public MainTabWindow_Chat(string convKey, string modeTitle)
        {
            _history = CoreServices.Locator.Get<RimAI.Core.Services.IHistoryWriteService>();
            _pidService = CoreServices.Locator.Get<IParticipantIdService>();
            _modeTitle = string.IsNullOrWhiteSpace(modeTitle) ? "聊天" : modeTitle.Trim();
            _config = CoreServices.Locator.Get<RimAI.Core.Infrastructure.Configuration.IConfigurationService>();

            forcePause = false;
            draggable = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = false;

            _convKeyInput = convKey ?? string.Empty;
            _ = EnsureExactLoadOrCreateAsync();
        }

        public override Vector2 InitialSize => new Vector2(920f, 720f);

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            // 标题行：左侧头像 + 标题
            DrawHeader(inRect, ref y);
            y += RowSpacing;

            // 子标题行：左人格状态 + 右按钮组（任命/历史）
            DrawSubHeader(inRect, ref y);
            y += RowSpacing;

            // 历史列表 + 指示灯 + 输入栏（历史区域增加圆角边框）
            float inputHeight = 60f; // 略微降低输入区与按钮高度
            float indicatorsHeight = IndicatorsRowHeight;
            var historyOuterRect = new Rect(inRect.x, y, inRect.width, inRect.height - (y - inRect.y) - (inputHeight + RowSpacing + indicatorsHeight + RowSpacing));
            Widgets.DrawMenuSection(historyOuterRect);
            var historyRect = new Rect(historyOuterRect.x + 6f, historyOuterRect.y + 6f, historyOuterRect.width - 12f, historyOuterRect.height - 12f);
            var indicatorsRect = new Rect(inRect.x, historyOuterRect.yMax + RowSpacing, inRect.width, indicatorsHeight);
            var inputRect = new Rect(inRect.x, indicatorsRect.yMax + RowSpacing, inRect.width, inputHeight);
            FlushProgressLines();
            FlushStreamDeltas();
            DrawHistory(historyRect);
            DrawIndicatorsBar(indicatorsRect);
            DrawInputBar(inputRect);
        }

        public override void PreClose()
        {
            // 关闭时：尽量中断在途任务并退订进度，清理状态
                try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            try { UnsubscribeProgress(); } catch { }
                        _streamAssistantBuffer = null;
                        _pendingPlayerMessage = null;
                        _isSending = false;
            base.PreClose();
        }
    }
}


