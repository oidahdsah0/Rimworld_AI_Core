#nullable enable
using System;
using RimAI.Core.Settings;

namespace RimAI.Core.Infrastructure.Configuration
{
    public interface IConfigurationService
    {
        CoreConfig Current { get; }
        event Action<CoreConfig>? OnConfigurationChanged;
        void Reload();
    }
}
