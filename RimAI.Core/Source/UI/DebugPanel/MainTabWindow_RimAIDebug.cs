using UnityEngine;
using RimWorld;
using Verse;
using RimAI.Core.Infrastructure.Configuration;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.UI.DebugPanel
{
    /// <summary>
    /// Developer debug panel. P0: Ping; P1: Reload Config.
    /// </summary>
    public class MainTabWindow_RimAIDebug : MainTabWindow
    {
        private const float ButtonHeight = 30f;
        private const float ButtonWidth = 140f;

        public override Vector2 RequestedTabSize => new Vector2(600f, 400f);

        public MainTabWindow_RimAIDebug()
        {
            this.forcePause = false;
            this.preventCameraMotion = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GUI.BeginGroup(inRect);
            float y = 10f;
            var pingRect = new Rect(10f, y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(pingRect, "Ping"))
            {
                Messages.Message("RimAI Core Loaded", MessageTypeDefOf.PositiveEvent, historical: false);
            }

            y += ButtonHeight + 4f;
            var reloadRect = new Rect(10f, y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(reloadRect, "Reload Config"))
            {
                var cfgSvc = ServiceContainer.Resolve<IConfigurationService>();
                cfgSvc.Reload();
                Messages.Message($"Config reloaded. Temp={cfgSvc.Current.LLM.Temperature}", MessageTypeDefOf.PositiveEvent, false);
            }

            y += ButtonHeight + 4f;
            var chatRect = new Rect(10f, y, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(chatRect, "Chat Echo"))
            {
                _ = SendEchoAsync();
            }
            GUI.EndGroup();
        }

        private async System.Threading.Tasks.Task SendEchoAsync()
        {
            var llm = ServiceContainer.Resolve<Modules.LLM.ILLMService>();
            var messages = new System.Collections.Generic.List<RimAI.Framework.Contracts.ChatMessage>
            {
                new RimAI.Framework.Contracts.ChatMessage { Role = "system", Content = "You are a helpful assistant." },
                new RimAI.Framework.Contracts.ChatMessage { Role = "user", Content = "Echo this" }
            };
            var req = new RimAI.Framework.Contracts.UnifiedChatRequest { Messages = messages };
            try
            {
                var response = await llm.GetResponseAsync(req);
                Messages.Message($"LLM: {response}", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (System.Exception ex)
            {
                Messages.Message($"LLM Error: {ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}
