using System;
using RimAI.Core.Contracts.Data;

namespace RimAI.Core.Contracts.Services
{
    public interface IConfigurationService
    {
        CoreConfig Current { get; }

        event Action<CoreConfig> OnConfigurationChanged;

        void Reload();
    }
}