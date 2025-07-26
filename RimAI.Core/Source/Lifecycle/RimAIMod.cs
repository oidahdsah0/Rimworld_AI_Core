using RimAI.Core.Architecture.DI;
using RimAI.Core.Contracts.Services;
using RimAI.Core.Services;
using Verse;

namespace RimAI.Core.Lifecycle
{
    public class RimAIMod : Mod
    {
        public RimAIMod(ModContentPack content) : base(content)
        {
            Log.Message("[RimAI.Core] Initializing...");

            var container = new ServiceContainer();

            CoreServices.Container = container;

            container.Register<IConfigurationService, ConfigurationService>();

            container.Register<ServiceContainer, ServiceContainer>(container);

            Log.Message("[RimAI.Core] Initialization Complete.");
        }
    }
}