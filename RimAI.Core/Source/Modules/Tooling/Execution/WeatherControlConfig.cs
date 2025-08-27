namespace RimAI.Core.Source.Modules.Tooling.Execution
{
    // Centralized knobs for weather control tool (single source of truth)
    internal static class WeatherControlConfig
    {
        // Execute cooldown (days)
        public static int CooldownDays = 5;
        // Forced duration window (days)
        public static int MinDurationDays = 1;
        public static int MaxDurationDays = 3;
        // Canonical allowed weather names (DefName or label-like canonical keys)
        public static readonly string[] AllowedWeathers = new[]
        {
            "Clear", "Fog", "Rain", "DryThunderstorm", "RainyThunderstorm", "FoggyRain", "SnowHard", "SnowGentle"
        };
        // Fuzzy match threshold [0..1]
        public static double FuzzyMinScore = 0.65;
    }
}
