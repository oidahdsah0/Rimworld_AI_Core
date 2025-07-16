using Verse;

namespace RimAI.Core.Core
{
    /// <summary>
    /// Settings for the RimAI Core mod.
    /// </summary>
    public class RimAICoreSettings : ModSettings
    {
        /// <summary>
        /// Whether the Governor officer is enabled.
        /// </summary>
        public bool enableGovernor = true;

        /// <summary>
        /// Whether the Military Officer is enabled.
        /// </summary>
        public bool enableMilitaryOfficer = true;

        /// <summary>
        /// Whether the Logistics Officer is enabled.
        /// </summary>
        public bool enableLogisticsOfficer = true;

        /// <summary>
        /// Whether the direct command interface is enabled.
        /// </summary>
        public bool enableDirectCommands = true;

        /// <summary>
        /// Whether the W.I.F.E. system is enabled.
        /// </summary>
        public bool enableWIFESystem = true;

        /// <summary>
        /// Default material depth level for AI analysis (1-5).
        /// </summary>
        public int defaultMaterialDepth = 3;

        /// <summary>
        /// Whether to show debug information in logs.
        /// </summary>
        public bool enableDebugLogging = false;

        /// <summary>
        /// This method is called by RimWorld to save and load the mod's settings.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref enableGovernor, "RimAICore_enableGovernor", true);
            Scribe_Values.Look(ref enableMilitaryOfficer, "RimAICore_enableMilitaryOfficer", true);
            Scribe_Values.Look(ref enableLogisticsOfficer, "RimAICore_enableLogisticsOfficer", true);
            Scribe_Values.Look(ref enableDirectCommands, "RimAICore_enableDirectCommands", true);
            Scribe_Values.Look(ref enableWIFESystem, "RimAICore_enableWIFESystem", true);
            Scribe_Values.Look(ref defaultMaterialDepth, "RimAICore_defaultMaterialDepth", 3);
            Scribe_Values.Look(ref enableDebugLogging, "RimAICore_enableDebugLogging", false);
        }
    }
}
