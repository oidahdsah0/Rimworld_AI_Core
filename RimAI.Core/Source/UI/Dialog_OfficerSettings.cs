using System;
using UnityEngine;
using Verse;
using RimAI.Core.Architecture;
using System.Threading.Tasks;
using System.Text;
using RimAI.Core.Architecture.Interfaces;

namespace RimAI.Core.UI
{
    public class Dialog_OfficerSettings : Window
    {
        private IAIOfficer _officer;
        private Vector2 _scrollPosition = Vector2.zero;
        private string _debugInfo = "";

        public Dialog_OfficerSettings(IAIOfficer officer)
        {
            _officer = officer;
            forcePause = true;
            doCloseButton = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        public override Vector2 InitialSize => new Vector2(640f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // Title
            Text.Font = GameFont.Medium;
            listing.Label($"{_officer.Name} Settings");
            Text.Font = GameFont.Small;
            listing.GapLine();
            listing.Gap();

            // Status
            listing.Label($"Status: {_officer.GetStatus()}");
            listing.Gap(12f);

            // Description
            Widgets.Label(listing.GetRect(40f), _officer.Description);
            listing.Gap(12f);

            if (listing.ButtonText("Generate Debug Info"))
            {
                GenerateDebugInfo();
            }

            if (!string.IsNullOrEmpty(_debugInfo))
            {
                Rect textRect = listing.GetRect(200f);
                Widgets.TextArea(textRect, _debugInfo, true);
            }

            listing.End();
        }

        private void GenerateDebugInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- Debug Info for {_officer.Name} ---");
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Is Available: {_officer.IsAvailable}");
            sb.AppendLine($"Status: {_officer.GetStatus()}");
            sb.AppendLine("\n--- Global Services Status ---");
            sb.AppendLine(CoreServices.GetServiceStatusReport());
            _debugInfo = sb.ToString();
        }
    }
}
