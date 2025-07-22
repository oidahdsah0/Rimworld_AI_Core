using System.Threading.Tasks;

namespace RimAI.Core.Architecture.Interfaces
{
    /// <summary>
    /// Represents an object that can persist its data as part of a game save.
    /// This integrates with RimWorld's native Scribe system.
    /// </summary>
    public interface IPersistable
    {
        /// <summary>
        /// This method is called by the Scribe system during saving and loading.
        /// Implement this to define what data gets written to or read from the save file.
        /// </summary>
        void ExposeData();
    }

    /// <summary>
    /// A comprehensive service for managing both per-save data and global mod settings.
    /// </summary>
    public interface IPersistenceService
    {
        // --- Per-Save Data Management (integrates with Scribe system) ---

        /// <summary>
        /// Registers an object to be included in the game's save/load cycle.
        /// </summary>
        /// <param name="persistable">The object that implements IPersistable.</param>
        void RegisterPersistable(IPersistable persistable);

        /// <summary>
        /// Unregisters an object from the game's save/load cycle.
        /// </summary>
        /// <param name="persistable">The object to remove.</param>
        void UnregisterPersistable(IPersistable persistable);

        /// <summary>
        /// Called by the game's core component to trigger the ExposeData method on all registered objects.
        /// </summary>
        void ExposeAllRegisteredData();


        // --- Global Settings Management (independent of game saves) ---

        /// <summary>
        /// Asynchronously saves a global setting to a file.
        /// </summary>
        /// <typeparam name="T">The type of the setting object.</typeparam>
        /// <param name="key">A unique key for the setting.</param>
        /// <param name="setting">The setting object to save.</param>
        Task SaveGlobalSettingAsync<T>(string key, T setting);

        /// <summary>
        /// Asynchronously loads a global setting from a file.
        /// </summary>
        /// <typeparam name="T">The type of the setting object.</typeparam>
        /// <param name="key">The unique key for the setting.</param>
        /// <returns>The loaded setting object, or the default value for the type if not found.</returns>
        Task<T> LoadGlobalSettingAsync<T>(string key);
    }
} 