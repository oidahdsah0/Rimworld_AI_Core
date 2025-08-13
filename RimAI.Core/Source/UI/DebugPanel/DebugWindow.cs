using System;
using System.Linq;
using RimAI.Core.Contracts.Config;
using RimAI.Core.Source.Boot;
using RimAI.Core.Source.Infrastructure;
using RimAI.Core.Source.Infrastructure.Configuration;
using RimAI.Core.Source.Modules.LLM;
using RimAI.Core.Source.UI.DebugPanel.Parts;
using UnityEngine;
using Verse;

namespace RimAI.Core.Source.UI.DebugPanel
{
    public sealed class DebugWindow : Window
    {
        private readonly IConfigurationService _configService;
        private readonly ServiceContainer _container;
        private Vector2 _scrollPos = Vector2.zero;
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
            _configPreviewJson = JsonPreview();
        }

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

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
            var viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(200f, Text.CalcHeight(_configPreviewJson, outRect.width - 16f)));
            Widgets.BeginScrollView(outRect, ref _scrollPos, viewRect);
            Widgets.Label(viewRect, _configPreviewJson);
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

            var llm = _container.Resolve<ILLMService>();
            LLM_PingButton.Draw(listing, llm);
            LLM_StreamDemoButton.Draw(listing, llm);
            LLM_JsonModeDemoButton.Draw(listing, llm);
            LLM_EmbeddingTestButton.Draw(listing, llm);
            LLM_InvalidateCacheButton.Draw(listing, llm);

            listing.End();
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
    }
}


