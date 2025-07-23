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

        private List<IPersistable> _persistables = new List<IPersistable>();
        private string _saveFilePath;

        private readonly List<IPersistable> _registeredPersistables = new List<IPersistable>();
        private readonly string _globalSettingsFolderPath;
        
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        public PersistenceService()
        {
            // We create a unique data file inside the standard RimWorld save directory.
            _saveFilePath = Path.Combine(GenFilePaths.SaveDataFolderPath, "RimAICoreData.xml");
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
            if (!_persistables.Contains(persistable))
            {
                _persistables.Add(persistable);
                Log.Message($"[PersistenceService] Registered {persistable.GetType().Name}.");
            }
        }

        public void UnregisterPersistable(IPersistable persistable)
        {
            _registeredPersistables.Remove(persistable);
        }

        public void ExposeAllRegisteredData()
        {
            // This is the core of RimWorld's Scribe system. It will look for the list
            // and save/load each item in it.
            Scribe_Collections.Look(ref _persistables, "persistables", LookMode.Deep);
        }

        public void Save()
        {
            try
            {
                // Correct way to save custom data to a separate file.
                Scribe.saver.InitSaving(_saveFilePath, "RimAICore");
                ExposeAllRegisteredData();
                Scribe.saver.FinalizeSaving();
                Log.Message($"[PersistenceService] Data saved to {_saveFilePath}");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[PersistenceService] Error saving data: {ex}");
            }
        }

        public void Load()
        {
            if (!File.Exists(_saveFilePath))
            {
                Log.Message($"[PersistenceService] No data file found at {_saveFilePath}. Skipping load.");
                return;
            }

            try
            {
                // Correct way to load custom data from a separate file.
                Scribe.loader.InitLoading(_saveFilePath);
                ExposeAllRegisteredData();
                Scribe.loader.FinalizeLoading();
                Log.Message($"[PersistenceService] Data loaded from {_saveFilePath}");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[PersistenceService] Error loading data: {ex}");
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