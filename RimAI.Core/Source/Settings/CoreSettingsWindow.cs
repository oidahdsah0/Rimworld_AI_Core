using UnityEngine;
using Verse;
using RimAI.Core.Architecture;
using RimAI.Core.Settings;
using RimWorld;

namespace RimAI.Core.Settings
{
    public class CoreSettingsWindow
    {
        // This class is a remnant and its direct drawing logic is simplified.
        // It's assumed the main interaction is through Dialog_OfficerSettings.
        public void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("⚙️ System Settings");
            listing.Gap(12f);

            // Player Nickname
            Rect nicknameRect = listing.GetRect(30f);
            Widgets.Label(nicknameRect.LeftPart(0.4f), "NPC如何称呼您？");
            SettingsManager.Settings.Player.Nickname = Widgets.TextField(nicknameRect.RightPart(0.6f), SettingsManager.Settings.Player.Nickname);
            listing.Gap(6f);

            // Core Framework Status
            listing.Label("📊 System Status");
            var statusInfo = CoreServices.GetServiceStatusReport();
            var statusRect = listing.GetRect(120f);
            Widgets.TextArea(statusRect, statusInfo, true);
            listing.Gap(12f);

            if (listing.ButtonText("Force Re-initialize Services"))
            {
                ServiceContainer.Initialize(); // Correctly call the static method
                Messages.Message("RimAI services re-initialized.", MessageTypeDefOf.PositiveEvent);
            }

            listing.End();
        }
    }
}
