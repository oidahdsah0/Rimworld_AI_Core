using RimAI.Core.Settings;
using UnityEngine;
using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// RimAI Core Mod主类
    /// </summary>
    public class RimAICoreMod : Mod
    {
        private CoreSettings _settings;

        public RimAICoreMod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<CoreSettings>();
            
            Log.Message("[RimAICoreMod] RimAI Core mod loaded");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var settingsWindow = new CoreSettingsWindow();
            settingsWindow.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimAI Core";
        }
    }
}
