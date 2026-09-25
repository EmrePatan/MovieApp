namespace MovieApp.Application.Configuration;

public sealed class AiRecommendationOptions
{
    public const string SectionName = "AiRecommendations";

    public int UserDailyMessageLimit { get; set; } = 3;

    public int GlobalDailyRequestCap { get; set; } = 400;

    public int RpmLimit { get; set; } = 12;

    public int SuggestionCount { get; set; } = 10;

    public int MaxReturnedCount { get; set; } = 5;

    public int SessionTtlHours { get; set; } = 24;

    public int TasteProfileCacheMinutes { get; set; } = 10;

    public int ExternalLlmChainBudgetSeconds { get; set; } = 20;

    public int DefaultProviderRequestTimeoutSeconds { get; set; } = 6;

    public GeminiAiRecommendationOptions Gemini { get; set; } = new();

    public GroqAiRecommendationOptions Groq { get; set; } = new();

    public OpenRouterAiRecommendationOptions OpenRouter { get; set; } = new();

    public CloudflareWorkersAiRecommendationOptions Cloudflare { get; set; } = new();

    public AiRecommendationEntitlementOptions Entitlement { get; set; } = new();

    public int ResolveProviderTimeoutSeconds(int configuredTimeoutSeconds) =>
        configuredTimeoutSeconds > 0
            ? configuredTimeoutSeconds
            : DefaultProviderRequestTimeoutSeconds;
}

public sealed class GeminiAiRecommendationOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "gemini-3.1-flash-lite";

    public bool Enabled { get; set; }

    public int RequestTimeoutSeconds { get; set; }
}

public sealed class GroqAiRecommendationOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public int RequestTimeoutSeconds { get; set; }

    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
}

public sealed class OpenRouterAiRecommendationOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public int RequestTimeoutSeconds { get; set; }

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
}

public sealed class CloudflareWorkersAiRecommendationOptions
{
    public string AccountId { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public int RequestTimeoutSeconds { get; set; }
}

public sealed class AiRecommendationEntitlementOptions
{
    public bool AllowDevelopmentBypass { get; set; }

    public HashSet<Guid> PremiumUserIds { get; set; } = [];
}
