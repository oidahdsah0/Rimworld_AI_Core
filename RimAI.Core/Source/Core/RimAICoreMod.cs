using UnityEngine;
using Verse;
using RimAI.Framework.Core;
using RimAI.Framework.LLM;

namespace RimAI.Core.Core
{
    /// <summary>
    /// The main Mod class for RimAI Core.
    /// This class handles the integration with RimAI Framework and provides
    /// the core functionality for the AI colony management system.
    /// </summary>
    public class RimAICoreMod : Mod
    {
        /// <summary>
        /// A reference to our settings instance.
        /// </summary>
        public readonly RimAICoreSettings settings;

        /// <summary>
        /// Constructor for the Mod class.
        /// </summary>
        /// <param name="content">The ModContentPack which contains info about this mod.</param>
        public RimAICoreMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<RimAICoreSettings>();
            
            // Initialize the AI Core system
            Log.Message("RimAI Core: Initializing AI Core system...");
            
            // Verify RimAI Framework is available
            if (LLMManager.Instance == null)
            {
                Log.Error("RimAI Core: RimAI Framework is not available. Please ensure it is installed and loaded.");
                return;
            }
            
            Log.Message("RimAI Core: Successfully initialized with RimAI Framework.");
        }

        /// <summary>
        /// The name of the mod in the settings list.
        /// </summary>
        public override string SettingsCategory()
        {
            return "RimAI.Core.Settings.Category".Translate();
        }

        /// <summary>
        /// This method is called when the user opens the settings window for this mod.
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // AI Core Settings Header
            listingStandard.Label("RimAI.Core.Settings.Header".Translate());
            listingStandard.Gap();

            // Three-Officer System Settings
            listingStandard.Label("RimAI.Core.Settings.Officers.Header".Translate());
            listingStandard.CheckboxLabeled("RimAI.Core.Settings.Officers.Governor".Translate(), ref settings.enableGovernor);
            listingStandard.CheckboxLabeled("RimAI.Core.Settings.Officers.Military".Translate(), ref settings.enableMilitaryOfficer);
            listingStandard.CheckboxLabeled("RimAI.Core.Settings.Officers.Logistics".Translate(), ref settings.enableLogisticsOfficer);
            listingStandard.Gap();

            // Direct Command Interface Settings
            listingStandard.Label("RimAI.Core.Settings.Commands.Header".Translate());
            listingStandard.CheckboxLabeled("RimAI.Core.Settings.Commands.Enable".Translate(), ref settings.enableDirectCommands);
            listingStandard.Gap();

            // W.I.F.E. System Settings
            listingStandard.Label("RimAI.Core.Settings.WIFE.Header".Translate());
            listingStandard.CheckboxLabeled("RimAI.Core.Settings.WIFE.Enable".Translate(), ref settings.enableWIFESystem);
            listingStandard.Gap();

            // Material Depth Settings
            listingStandard.Label("RimAI.Core.Settings.Depth.Header".Translate());
            listingStandard.Label("RimAI.Core.Settings.Depth.Level".Translate() + ": " + settings.defaultMaterialDepth);
            settings.defaultMaterialDepth = (int)listingStandard.Slider(settings.defaultMaterialDepth, 1, 5);
            listingStandard.Gap();

            // Status Information
            listingStandard.Label("RimAI.Core.Settings.Status.Header".Translate());
            if (LLMManager.Instance != null)
            {
                listingStandard.Label("RimAI.Core.Settings.Status.FrameworkConnected".Translate());
            }
            else
            {
                listingStandard.Label("RimAI.Core.Settings.Status.FrameworkNotFound".Translate());
            }

            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
        }
    }
}
