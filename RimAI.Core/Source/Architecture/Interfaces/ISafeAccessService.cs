using System.Collections.Generic;
using RimWorld;
using Verse;
using System; // For Func

namespace RimAI.Core.Architecture.Interfaces
{
    public interface ISafeAccessService
    {
        List<Pawn> GetColonistsSafe(Map map, int maxRetries = 3);
        List<Pawn> GetPrisonersSafe(Map map, int maxRetries = 3);
        List<Pawn> GetAllPawnsSafe(Map map, int maxRetries = 3);
        List<Building> GetBuildingsSafe(Map map, int maxRetries = 3);
        List<Thing> GetThingsSafe(Map map, ThingDef thingDef, int maxRetries = 3);
        List<Thing> GetThingGroupSafe(Map map, ThingRequestGroup group, int maxRetries = 3);
        int GetColonistCountSafe(Map map, int maxRetries = 3);
        WeatherDef GetCurrentWeatherSafe(Map map, int maxRetries = 3);
        int GetTicksGameSafe(int maxRetries = 3);
        Season GetCurrentSeasonSafe(Map map, int maxRetries = 3);
        string GetStatusReport();

        // Add missing operation wrappers
        TResult SafePawnOperation<TResult>(List<Pawn> pawns, Func<List<Pawn>, TResult> operation, TResult fallbackValue, string operationName);
        TResult SafeBuildingOperation<TResult>(List<Building> buildings, Func<List<Building>, TResult> operation, TResult fallbackValue, string operationName);
        TResult SafeThingOperation<TResult>(List<Thing> things, Func<List<Thing>, TResult> operation, TResult fallbackValue, string operationName);
    }
} 