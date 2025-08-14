using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Infrastructure.Scheduler;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.Modules.Orchestration;
using RimAI.Core.Source.Modules.World;
using RimAI.Core.Source.UI.DebugPanel.Parts;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel
{
    public sealed class DebugWindow : Window
    {
        private readonly IConfigurationService _configService;
        private readonly ServiceContainer _container;
		private readonly ILLMService _llm;
        private readonly ISchedulerService _scheduler;
        private readonly IWorldDataService _world;
        private readonly RimAI.Core.Source.Modules.Tooling.IToolRegistryService _tooling;
        private readonly RimAI.Core.Source.Modules.Persistence.IPersistenceService _persistence;
        private readonly IOrchestrationService _orchestration;
		private Vector2 _scrollPos = Vector2.zero; // config preview
		private Vector2 _pageScrollPos = Vector2.zero; // whole page scroll

		// P2 Streaming live view state
		private volatile string _streamOutput = string.Empty;
		private Vector2 _streamScrollPos = Vector2.zero;
		private bool _streaming = false;
		private CancellationTokenSource _streamCts;
		private string _streamConv = string.Empty;
		private DateTime _streamStartUtc;
        private string _configPreviewJson = string.Empty;

        public override Vector2 InitialSize => new Vector2(700f, 600f);

		public DebugWindow()
        {
            doCloseX = true;
            draggable = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = true;

            _container = RimAICoreMod.Container;
            _configService = _container.Resolve<IConfigurationService>();
			_llm = _container.Resolve<ILLMService>();
            _scheduler = _container.Resolve<ISchedulerService>();
            _world = _container.Resolve<IWorldDataService>();
            _tooling = _container.Resolve<RimAI.Core.Source.Modules.Tooling.IToolRegistryService>();
            _orchestration = _container.Resolve<IOrchestrationService>();
            _persistence = _container.Resolve<RimAI.Core.Source.Modules.Persistence.IPersistenceService>();
            _configPreviewJson = JsonPreview();
        }

		public override void DoWindowContents(Rect inRect)
        {
			// 外层滚动容器（整页滚动）
			var viewHeightBase = 2000f; // 固定页面内容高度，流式区域已有自身滚动
			var pageViewRect = new Rect(0f, 0f, inRect.width - 16f, viewHeightBase);
			Widgets.BeginScrollView(inRect, ref _pageScrollPos, pageViewRect);

			var listing = new Listing_Standard();
			listing.Begin(pageViewRect);

            Text.Font = GameFont.Medium;
            listing.Label("[RimAI.Core][P1] Debug Panel");
            Text.Font = GameFont.Small;
            listing.GapLine();

            // Ping Section
            if (listing.ButtonText("Ping (P1)"))
            {
                var version = _configService.Current.Version;
                Log.Message($"[RimAI.Core][P1] pong | services={RimAICoreMod.Container.GetKnownServiceCount()} | version={version}");
            }

            // ResolveAll Section
            if (listing.ButtonText("ResolveAll"))
            {
                var health = _container.ResolveAllAndGetHealth();
                foreach (var h in health.OrderBy(h => h.ServiceName))
                {
                    var status = h.IsOk ? "OK" : "FAILED";
                    Log.Message($"[RimAI.Core][P1] {status} {h.ServiceName} (constructed in {h.ConstructionElapsed.TotalMilliseconds} ms){(h.ErrorMessage != null ? " | " + h.ErrorMessage : string.Empty)}");
                }
            }

            // Config Preview + Reload
            listing.Label("Config Snapshot Preview:");
            var outRect = listing.GetRect(200f);
            Widgets.DrawBoxSolid(outRect, new Color(0f, 0f, 0f, 0.1f));
			var configViewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(200f, Text.CalcHeight(_configPreviewJson, outRect.width - 16f)));
			Widgets.BeginScrollView(outRect, ref _scrollPos, configViewRect);
			Widgets.Label(configViewRect, _configPreviewJson);
            Widgets.EndScrollView();

            if (listing.ButtonText("Reload Config"))
            {
                // P1: simple reload
                ( _configService as ConfigurationService )?.Reload();
                _configPreviewJson = JsonPreview();
                var snap = _configService.Current;
                Log.Message($"[RimAI.Core][P1] Config Reloaded (version={snap.Version}, locale={snap.Locale}, at={DateTime.UtcNow:O})");
            }

            listing.GapLine();
			Text.Font = GameFont.Medium;
            listing.Label("[RimAI.Core][P2] LLM Gateway");
            Text.Font = GameFont.Small;
            listing.GapLine();

			LLM_PingButton.Draw(listing, _llm);
			LLM_StreamDemoButton.Draw(listing, _llm);
			LLM_JsonModeDemoButton.Draw(listing, _llm);
			LLM_EmbeddingTestButton.Draw(listing, _llm);
			LLM_InvalidateCacheButton.Draw(listing, _llm);

			// P2: Streaming Live View（UI 真正流式显示，仅 UI 允许流式）
			listing.GapLine();
			Text.Font = GameFont.Medium;
			listing.Label("[RimAI.Core][P2] Streaming Live View");
			Text.Font = GameFont.Small;
			if (!_streaming)
			{
				if (listing.ButtonText("Start Stream (中文笑话示例)"))
				{
					_streamOutput = string.Empty;
					_streamConv = "ui-stream-" + DateTime.UtcNow.Ticks;
					_streamStartUtc = DateTime.UtcNow;
					_streamCts = new CancellationTokenSource();
					_streaming = true;
					_ = Task.Run(() => RunStreamingDemoAsync(_streamCts.Token));
				}
			}
			else
			{
				listing.Label($"Streaming... conv={_streamConv} elapsed={(DateTime.UtcNow - _streamStartUtc).TotalMilliseconds:F0} ms");
				if (listing.ButtonText("Cancel Stream"))
				{
					try { _streamCts?.Cancel(); } catch { }
				}
			}

			// 输出区域（可滚动）
			var outRect2 = listing.GetRect(200f);
			Widgets.DrawBoxSolid(outRect2, new Color(0f, 0f, 0f, 0.08f));
			var viewRect2 = new Rect(0f, 0f, outRect2.width - 16f, Math.Max(200f, Text.CalcHeight(_streamOutput ?? string.Empty, outRect2.width - 16f) + 8f));
			Widgets.BeginScrollView(outRect2, ref _streamScrollPos, viewRect2);
			Widgets.Label(viewRect2, _streamOutput ?? string.Empty);
			Widgets.EndScrollView();


            // P3 Panels
			listing.GapLine();
			P3_SchedulerPanel.Draw(listing, _scheduler);
			listing.GapLine();
			P3_WorldDataPanel.Draw(listing, _world);

            // P4 Tooling panels
            listing.GapLine();
            RimAI.Core.Source.UI.DebugPanel.Parts.P4_ToolIndexPanel.Draw(listing, _tooling);
            listing.GapLine();
            RimAI.Core.Source.UI.DebugPanel.Parts.P4_ToolRunner.Draw(listing, _tooling);
            listing.GapLine();
            P5_OrchestrationPanel.Draw(listing, _orchestration);

            // P6 Persistence
            listing.GapLine();
            RimAI.Core.Source.UI.DebugPanel.Parts.P6_PersistencePanel.Draw(listing, _persistence);

            listing.End();
			Widgets.EndScrollView();
        }

		private string JsonPreview()
        {
            if (_configService is ConfigurationService impl)
            {
                return impl.GetSnapshotJsonPretty();
            }
            var snap = _configService.Current;
            return $"Version: {snap.Version}\nLocale: {snap.Locale}\nDebugPanelEnabled: {snap.DebugPanelEnabled}\nVerboseLogs: {snap.VerboseLogs}";
        }

		private async Task RunStreamingDemoAsync(CancellationToken ct)
		{
			try
			{
				var firstUiAppended = false;
				await foreach (var r in _llm.StreamResponseAsync(_streamConv,
					"You are a helpful assistant.",
					"请用中文给我讲一个关于机器人和猫的短笑话。",
					ct))
				{
					if (!r.IsSuccess)
					{
						_streamOutput += $"\n[error] {r.Error}";
						break;
					}
					var chunk = r.Value;
					if (!string.IsNullOrEmpty(chunk.ContentDelta))
					{
						_streamOutput += chunk.ContentDelta;
						if (!firstUiAppended)
						{
							firstUiAppended = true;
							var elapsedMs = (DateTime.UtcNow - _streamStartUtc).TotalMilliseconds;
							Verse.Log.Message($"[RimAI.Core][P2.UI][Obs] first UI append at {elapsedMs:F0} ms conv={RimAI.Core.Source.Modules.LLM.LlmLogging.HashConversationId(_streamConv)}");
						}
					}
					if (!string.IsNullOrEmpty(chunk.FinishReason))
					{
						_streamOutput += $"\n\n[finish] {chunk.FinishReason}";
						break;
					}
				}
			}
			catch (OperationCanceledException)
			{
				_streamOutput += "\n[cancelled]";
			}
			catch (Exception ex)
			{
				_streamOutput += $"\n[exception] {ex.Message}";
			}
			finally
			{
				_streaming = false;
				try { _streamCts?.Dispose(); } catch { }
				_streamCts = null;
			}
		}
    }
}


