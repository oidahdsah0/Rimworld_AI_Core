using Verse;

namespace RimAI.Core
{
    /// <summary>
    /// Core 模块的 ModSettings 占位实现，未来可扩展真实字段并在 ConfigurationService 使用。
    /// </summary>
    public class RimAISettings : ModSettings
    {
        public double Temperature = 0.7;
        public string ApiKey = string.Empty;
        public int CacheDurationMinutes = 5;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Temperature, "Temperature", 0.7);
            Scribe_Values.Look(ref ApiKey, "ApiKey", string.Empty);
            Scribe_Values.Look(ref CacheDurationMinutes, "CacheDurationMinutes", 5);
        }
    }
}