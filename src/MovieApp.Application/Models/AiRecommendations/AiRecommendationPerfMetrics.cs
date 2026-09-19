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

    public int TmdbResolutionCalls { get; set; }

    public long SessionSaveMs { get; set; }

    public long QuotaCommitMs { get; set; }

    public int DbRoundTrips { get; set; }

    public int SuggestionCount { get; set; }

    public int GeminiSuggestionCount { get; set; }

    public int ReturnedCount { get; set; }
}
