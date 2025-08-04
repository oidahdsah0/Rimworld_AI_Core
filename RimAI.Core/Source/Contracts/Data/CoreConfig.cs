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
        public ResilienceConfig Resilience { get; init; } = new();
    }

    public class ResilienceConfig
    {
        public int MaxRetries { get; init; } = 3;
        public int CircuitBreakerFailureThreshold { get; init; } = 5;
        public int CircuitBreakerWindowSeconds { get; init; } = 60;
        public int CircuitBreakerCooldownSeconds { get; init; } = 300;
    }

    public class CacheConfig
    {
        public int CacheDurationMinutes { get; init; } = 5;
    }
}