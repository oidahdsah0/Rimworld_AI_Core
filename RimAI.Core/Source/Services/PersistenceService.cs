using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RimAI.Core.Architecture.Interfaces;
using Verse;

namespace RimAI.Core.Services
{
    public class PersistenceService : IPersistenceService
    {
        private static readonly Lazy<PersistenceService> _instance = new Lazy<PersistenceService>(() => new PersistenceService());
        public static IPersistenceService Instance => _instance.Value;

        private readonly List<IPersistable> _registeredPersistables = new List<IPersistable>();
        private readonly string _globalSettingsFolderPath;
        
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        private PersistenceService()
        {
            _globalSettingsFolderPath = Path.Combine(GenFilePaths.ConfigFolderPath, "RimAI.Core");
            try
            {
                Directory.CreateDirectory(_globalSettingsFolderPath);
            }
            catch (Exception ex)
            {
                Log.Error($"[PersistenceService] Could not create global settings directory at {_globalSettingsFolderPath}. Reason: {ex.Message}");
            }
        }

        // --- Per-Save Data Management ---

        public void RegisterPersistable(IPersistable persistable)
        {
            if (!_registeredPersistables.Contains(persistable))
            {
                _registeredPersistables.Add(persistable);
            }
        }

        public void UnregisterPersistable(IPersistable persistable)
        {
            _registeredPersistables.Remove(persistable);
        }

        public void ExposeAllRegisteredData()
        {
            foreach (var persistable in _registeredPersistables)
            {
                try
                {
                    persistable.ExposeData();
                }
                catch (Exception ex)
                {
                    Log.Error($"[PersistenceService] Error while exposing data for {persistable.GetType().Name}: {ex.Message}");
                }
            }
        }

        // --- Global Settings Management ---

        public async Task SaveGlobalSettingAsync<T>(string key, T setting)
        {
            try
            {
                var filePath = GetGlobalSettingsFilePath(key);
                var json = JsonConvert.SerializeObject(setting, _jsonSettings);
                await Task.Run(() => File.WriteAllText(filePath, json));
            }
            catch (Exception ex)
            {
                Log.Error($"[PersistenceService] Failed to save global setting '{key}'. Reason: {ex.Message}");
            }
        }

        public async Task<T> LoadGlobalSettingAsync<T>(string key)
        {
            var filePath = GetGlobalSettingsFilePath(key);
            if (!File.Exists(filePath))
            {
                return default;
            }

            try
            {
                var json = await Task.Run(() => File.ReadAllText(filePath));
                return JsonConvert.DeserializeObject<T>(json, _jsonSettings);
            }
            catch (Exception ex)
            {
                Log.Error($"[PersistenceService] Failed to load global setting '{key}'. Reason: {ex.Message}. Returning default value.");
                return default;
            }
        }

        private string GetGlobalSettingsFilePath(string key)
        {
            return Path.Combine(_globalSettingsFolderPath, $"{key}.json");
        }
    }
} 