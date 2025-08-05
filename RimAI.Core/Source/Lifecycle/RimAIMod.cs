using Verse;
using RimAI.Core.Infrastructure;

namespace RimAI.Core.Lifecycle
{
    /// <summary>
    /// RimWorld entrypoint class. Invoked by the engine when the mod is loaded.
    /// </summary>
    public class RimAIMod : Mod
    {
        public RimAIMod(ModContentPack content) : base(content)
        {
            // Initialise dependency container
            ServiceContainer.Init();

            // Simple ping to show the mod has loaded (matches DebugPanel Ping button)
            Log.Message("[RimAI.Core] RimAI Core loaded and ServiceContainer initialised.");
        }
    }
}
