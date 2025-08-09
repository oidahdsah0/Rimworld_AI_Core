namespace RimAI.Core.Contracts.Models
{
    /// <summary>
    /// 殖民地摘要（对外 DTO）。不依赖 Verse/Unity。
    /// </summary>
    public sealed class ColonySummary
    {
        public int ColonistCount { get; set; }
        public int FoodStockpile { get; set; }
        public string ThreatLevel { get; set; } = string.Empty;
    }
}


