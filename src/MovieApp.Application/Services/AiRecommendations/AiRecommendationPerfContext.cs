using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class AiRecommendationPerfContext : IAiRecommendationPerfContext
{
    public AiRecommendationPerfMetrics Metrics { get; } = new();

    public void RecordQuotaReserveMs(long milliseconds) =>
        Metrics.QuotaReserveMs = milliseconds;

    public void RecordTasteProfileCacheHit(long totalMs, long cacheLookupMs)
    {
        Metrics.TasteProfileCache = "HIT";
        Metrics.TasteProfileTotalMs = totalMs;
        Metrics.TasteProfileCacheLookupMs = cacheLookupMs;
        Metrics.TasteProfileDbRoundTrips = 0;
    }

    public void RecordTasteProfileCacheMiss(
        long totalMs,
        long cacheLookupMs,
        long buildCpuMs,
        long cacheWriteMs)
    {
        Metrics.TasteProfileCache = "MISS";
        Metrics.TasteProfileTotalMs = totalMs;
        Metrics.TasteProfileCacheLookupMs = cacheLookupMs;
        Metrics.TasteProfileBuildCpuMs = buildCpuMs;
        Metrics.TasteProfileCacheWriteMs = cacheWriteMs;
        Metrics.DbRoundTrips += Metrics.TasteProfileDbRoundTrips;
    }

    public void RecordTasteProfileQuery(string queryName, long milliseconds)
    {
        Metrics.TasteProfileDbRoundTrips++;

        switch (queryName)
        {
            case "Ratings":
                Metrics.TasteProfileRatingsMs = milliseconds;
                break;
            case "Favorites":
                Metrics.TasteProfileFavoritesMs = milliseconds;
                break;
            case "Watchlist":
                Metrics.TasteProfileWatchlistMs = milliseconds;
                break;
            case "WatchedMovies":
                Metrics.TasteProfileWatchedMoviesMs = milliseconds;
                break;
            case "WatchedTvGenres":
                Metrics.TasteProfileWatchedTvGenresMs = milliseconds;
                break;
        }
    }

    public void RecordSessionLoadMs(long milliseconds) =>
        Metrics.SessionLoadMs = milliseconds;

    public void RecordGeminiTimings(long totalMs, long httpMs, long parseMs)
    {
        Metrics.GeminiTotalMs = totalMs;
        Metrics.GeminiHttpMs = httpMs;
        Metrics.GeminiParseMs = parseMs;
    }

    public void RecordValidationTimings(long totalMs, long watchedIdsMs, long resolutionMs)
    {
        Metrics.ValidationMs = totalMs;
        Metrics.ValidationWatchedIdsMs = watchedIdsMs;
        Metrics.ValidationResolutionMs = resolutionMs;
        Metrics.DbRoundTrips++;
    }

    public void RecordValidationCatalogHit() =>
        Metrics.ValidationCatalogHits++;

    public void RecordValidationProviderFallback() =>
        Metrics.ValidationProviderFallbacks++;

    public void RecordValidationSearchFallback() =>
        Metrics.ValidationSearchFallbacks++;

    public void RecordValidationDedupHit() =>
        Metrics.ValidationDedupHits++;

    public void RecordTmdbResolutionCall() =>
        Metrics.TmdbResolutionCalls++;

    public void RecordSessionSaveMs(long milliseconds) =>
        Metrics.SessionSaveMs = milliseconds;

    public void RecordQuotaCommitMs(long milliseconds) =>
        Metrics.QuotaCommitMs = milliseconds;

    public void SetOutcome(string outcome) =>
        Metrics.Outcome = outcome;

    public void SetResultCounts(int configuredSuggestionCount, int geminiSuggestionCount, int returnedCount)
    {
        Metrics.SuggestionCount = configuredSuggestionCount;
        Metrics.GeminiSuggestionCount = geminiSuggestionCount;
        Metrics.ReturnedCount = returnedCount;
    }
}
