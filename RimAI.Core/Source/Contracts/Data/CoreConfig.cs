namespace RimAI.Core.Contracts.Data
{
    public class CoreConfig
    {
        public LLMConfig LLM { get; init; } = new();
        public CacheConfig Cache { get; init; } = new();

    }

    public class LLMConfig
    {
        public double Temperature { get; init; } = 1.2;
        public string ApiKey { get; init; } = string.Empty;
    }

    public class CacheConfig
    {
        public int CacheDurationMinutes { get; init; } = 5;
    }
}