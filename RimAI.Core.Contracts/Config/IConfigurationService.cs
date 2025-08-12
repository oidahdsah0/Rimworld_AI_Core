using System;

namespace RimAI.Core.Contracts.Config
{
    /// <summary>
    /// Stable, minimum external-facing configuration contract.
    /// Implementations live in RimAI.Core. Snapshot is immutable.
    /// </summary>
    public interface IConfigurationService
    {
        CoreConfigSnapshot Current { get; }

        /// <summary>
        /// Fired after configuration snapshot is replaced due to a reload.
        /// Subscribers must read the new snapshot and apply by themselves.
        /// </summary>
        event Action<CoreConfigSnapshot> OnConfigurationChanged;
    }
}


