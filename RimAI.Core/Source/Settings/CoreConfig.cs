namespace RimAI.Core.Settings
{
    public record LLMConfig(double Temperature = 0.7, string ApiKey = "");
    public record CacheConfig(int TTLSeconds = 300);

    public record CoreConfig
    {
        public LLMConfig LLM { get; init; } = new();
        public CacheConfig Cache { get; init; } = new();
    }
}
