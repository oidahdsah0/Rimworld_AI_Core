using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RimWorld;
using UnityEngine;
using Verse;
using RimAI.Core.Architecture.Interfaces;

namespace RimAI.Core.Services
{
    public class SafeAccessService : ISafeAccessService
    {
        private readonly Dictionary<string, int> _failureStats = new Dictionary<string, int>();
        private readonly object _statsLock = new object();

        public SafeAccessService()
        {
            // Public constructor, no static instance
        }

        private T SafeExecute<T>(Func<T> operation, string operationName, T fallbackValue, int maxRetries)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (operation != null)
                        return operation();
                    
                    return fallbackValue;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[SafeAccessService] Attempt {i + 1} failed for {operationName}: {ex.Message}");
                    if (i == maxRetries - 1)
                    {
                        lock(_statsLock)
                        {
                            if (!_failureStats.ContainsKey(operationName))
                            {
                                _failureStats[operationName] = 0;
                            }
                            _failureStats[operationName]++;
                        }
                        Log.Error($"[SafeAccessService] Operation {operationName} failed after {maxRetries} retries.");
                    }
                }
            }
            return fallbackValue;
        }

        // --- Implementation of all ISafeAccessService methods ---

        public List<Pawn> GetColonistsSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.mapPawns?.FreeColonists?.ToList(), "GetColonistsSafe", new List<Pawn>(), maxRetries);
        }

        public List<Pawn> GetPrisonersSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.mapPawns?.PrisonersOfColony?.ToList(), "GetPrisonersSafe", new List<Pawn>(), maxRetries);
        }

        public List<Pawn> GetAllPawnsSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.mapPawns?.AllPawns?.ToList(), "GetAllPawnsSafe", new List<Pawn>(), maxRetries);
        }

        public List<Building> GetBuildingsSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.listerBuildings?.allBuildingsColonist?.ToList(), "GetBuildingsSafe", new List<Building>(), maxRetries);
        }

        public List<Thing> GetThingsSafe(Map map, ThingDef thingDef, int maxRetries = 3)
        {
            return SafeExecute(() => map?.listerThings?.ThingsOfDef(thingDef)?.ToList(), "GetThingsSafe", new List<Thing>(), maxRetries);
        }

        public List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group, int maxRetries = 3)
        {
            return SafeExecute(() => map?.listerThings?.ThingsInGroup(group)?.ToList(), "GetThingGroupSafe", new List<Thing>(), maxRetries);
        }

        public int GetColonistCountSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.mapPawns?.FreeColonistsCount ?? 0, "GetColonistCountSafe", 0, maxRetries);
        }

        public WeatherDef GetCurrentWeatherSafe(Map map, int maxRetries = 3)
        {
            return SafeExecute(() => map?.weatherManager?.curWeather, "GetCurrentWeatherSafe", null, maxRetries);
        }
        
        public int GetTicksGameSafe(int maxRetries = 3)
        {
            return SafeExecute(() => GenTicks.TicksGame, "GetTicksGameSafe", 0, maxRetries);
        }

        public Season GetCurrentSeasonSafe(Map map, int maxRetries = 3)
        {
            float longitude = map != null ? Find.WorldGrid.LongLatOf(map.Tile).x : 0f;
            // Correctly pass longitude to GenDate.Season
            return SafeExecute(() => GenDate.Season(GenTicks.TicksGame, new Vector2(longitude, 0)), "GetCurrentSeasonSafe", Season.Undefined, maxRetries);
        }
        
        public string GetStatusReport()
        {
            lock (_statsLock)
            {
                if (_failureStats.Count == 0) return "SafeAccessService: All operations are successful.";
                return "SafeAccessService Failures: " + string.Join(", ", _failureStats.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            }
        }

        // --- Implementation of missing operation wrappers ---

        public TResult SafePawnOperation<TResult>(List<Pawn> pawns, Func<List<Pawn>, TResult> operation, TResult fallbackValue, string operationName)
            {
            return SafeExecute(() => operation(pawns ?? new List<Pawn>()), operationName, fallbackValue, 1);
            }

        public TResult SafeBuildingOperation<TResult>(List<Building> buildings, Func<List<Building>, TResult> operation, TResult fallbackValue, string operationName)
                {
            return SafeExecute(() => operation(buildings ?? new List<Building>()), operationName, fallbackValue, 1);
        }

        public TResult SafeThingOperation<TResult>(List<Thing> things, Func<List<Thing>, TResult> operation, TResult fallbackValue, string operationName)
        {
            return SafeExecute(() => operation(things ?? new List<Thing>()), operationName, fallbackValue, 1);
        }
    }
}
