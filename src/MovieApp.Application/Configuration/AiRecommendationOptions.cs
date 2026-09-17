namespace MovieApp.Application.Configuration;

public sealed class AiRecommendationOptions
{
    public const string SectionName = "AiRecommendations";

    public int UserDailyMessageLimit { get; set; } = 3;

    public int GlobalDailyRequestCap { get; set; } = 400;

    public int RpmLimit { get; set; } = 12;

    public int SuggestionCount { get; set; } = 8;

    public int MaxReturnedCount { get; set; } = 5;

    public int SessionTtlHours { get; set; } = 24;

    public int TasteProfileCacheMinutes { get; set; } = 10;

    public GeminiAiRecommendationOptions Gemini { get; set; } = new();

    public AiRecommendationEntitlementOptions Entitlement { get; set; } = new();
}

public sealed class GeminiAiRecommendationOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "gemini-3.1-flash-lite";

    public bool Enabled { get; set; }

    public int RequestTimeoutSeconds { get; set; } = 30;
}

public sealed class AiRecommendationEntitlementOptions
{
    public bool AllowDevelopmentBypass { get; set; }

    public HashSet<Guid> PremiumUserIds { get; set; } = [];
}
