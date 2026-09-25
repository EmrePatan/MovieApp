namespace MovieApp.Application.Models.AiRecommendations;

public sealed class AiRecommendationPerfMetrics
{
    public string Outcome { get; set; } = "Unknown";

    public long TotalMs { get; set; }

    public long QuotaReserveMs { get; set; }

    public string TasteProfileCache { get; set; } = "N/A";

    public long TasteProfileTotalMs { get; set; }

    public long TasteProfileCacheLookupMs { get; set; }

    public long TasteProfileBuildCpuMs { get; set; }

    public long TasteProfileCacheWriteMs { get; set; }

    public long TasteProfileRatingsMs { get; set; }

    public long TasteProfileFavoritesMs { get; set; }

    public long TasteProfileWatchlistMs { get; set; }

    public long TasteProfileWatchedMoviesMs { get; set; }

    public long TasteProfileWatchedTvGenresMs { get; set; }

    public int TasteProfileDbRoundTrips { get; set; }

    public long SessionLoadMs { get; set; }

    public long GeminiTotalMs { get; set; }

    public long GeminiHttpMs { get; set; }

    public long GeminiParseMs { get; set; }

    public long ValidationMs { get; set; }

    public long ValidationWatchedIdsMs { get; set; }

    public long ValidationResolutionMs { get; set; }

    public int ValidationCatalogHits { get; set; }

    public int ValidationProviderFallbacks { get; set; }

    public int ValidationSearchFallbacks { get; set; }

    public int ValidationDedupHits { get; set; }

    public int ValidationRejectedUnsupportedMediaType { get; set; }

    public int ValidationRejectedResolutionFailure { get; set; }

    public int ValidationRejectedResponseDuplicate { get; set; }

    public int ValidationRejectedSessionDuplicate { get; set; }

    public int ValidationRejectedWatched { get; set; }

    public int ValidationRejectedExcludedGenre { get; set; }

    public int ValidationRejectedRuntime { get; set; }

    public int ValidationRejectedYear { get; set; }

    public int TmdbResolutionCalls { get; set; }

    public long SessionSaveMs { get; set; }

    public long QuotaCommitMs { get; set; }

    public int DbRoundTrips { get; set; }

    public int SuggestionCount { get; set; }

    public int GeminiSuggestionCount { get; set; }

    public int ReturnedCount { get; set; }

    public string SuccessfulProvider { get; set; } = string.Empty;

    public int ProviderAttemptCount { get; set; }

    public int ProviderSkippedNotConfiguredCount { get; set; }

    public long LlmChainMs { get; set; }

    public long DeterministicMs { get; set; }

    public bool DeterministicFallbackUsed { get; set; }

    public bool LlmChainBudgetExhausted { get; set; }

    public string ProviderFailureSummary { get; set; } = string.Empty;
}
