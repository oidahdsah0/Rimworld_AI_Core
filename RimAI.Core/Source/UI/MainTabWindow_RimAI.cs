using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using RimAI.Core.Architecture.DI;
using RimAI.Core.Contracts.Services;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimAI.Core.UI
{
    /// <summary>
    /// 简易调试窗口：输入文本后实时流式显示 RimAI 回复。
    /// 仅用于开发验证，不代表最终 UI 外观。
    /// </summary>
    public class MainTabWindow_RimAI : MainTabWindow
    {
        private string _userInput = string.Empty;
        private readonly StringBuilder _responseBuilder = new();
        private Vector2 _scrollPos = Vector2.zero;
        private bool _isRunning;

        public override Vector2 InitialSize => new(600f, 400f);

        public override void DoWindowContents(Rect inRect)
        {
            var list = new Listing_Standard();
            list.Begin(inRect);

            list.Label("RimAI Assistant (开发原型)");
            list.GapLine();

            list.Label("输入:");
            _userInput = list.TextEntry(_userInput);

            if (list.ButtonText(_isRunning ? "处理中..." : "发送") && !_isRunning && !string.IsNullOrWhiteSpace(_userInput))
            {
                RunQuery(_userInput);
            }

            list.GapLine();
            list.Label("输出:");
            var outRect = list.GetRect(inRect.height - list.CurHeight - 20f);
            Widgets.BeginScrollView(outRect, ref _scrollPos, new Rect(0, 0, outRect.width - 16f, outRect.height + 100));
            Widgets.Label(new Rect(0, 0, outRect.width - 16f, outRect.height + 100), _responseBuilder.ToString());
            Widgets.EndScrollView();

            list.End();
        }

        private void RunQuery(string text)
        {
            _isRunning = true;
            _responseBuilder.Clear();

            var orchestrator = CoreServices.Container.Resolve<IOrchestrationService>();

            // 启动后台任务流式消费
            _ = Task.Run(async () =>
            {
                await foreach (var chunk in orchestrator.ExecuteToolAssistedQueryAsync(text, "你是 RimAI 助手"))
                {
                    if (chunk.IsSuccess && chunk.Value.ContentDelta != null)
                    {
                        _responseBuilder.Append(chunk.Value.ContentDelta);
                    }
                    else if (!chunk.IsSuccess)
                    {
                        _responseBuilder.Append($"\n[Error] {chunk.Error}");
                        break;
                    }
                }
                _isRunning = false;
            });
        }
    }
}
