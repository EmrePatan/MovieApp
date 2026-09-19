using MovieApp.Application.Abstractions.AiRecommendations;
using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Services.AiRecommendations;

public sealed class NullAiRecommendationPerfContext : IAiRecommendationPerfContext
{
    public static readonly NullAiRecommendationPerfContext Instance = new();

    public AiRecommendationPerfMetrics Metrics { get; } = new();

    public void RecordQuotaReserveMs(long milliseconds)
    {
    }

    public void RecordTasteProfileCacheHit(long totalMs, long cacheLookupMs)
    {
    }

    public void RecordTasteProfileCacheMiss(
        long totalMs,
        long cacheLookupMs,
        long buildCpuMs,
        long cacheWriteMs)
    {
    }

    public void RecordTasteProfileQuery(string queryName, long milliseconds)
    {
    }

    public void RecordSessionLoadMs(long milliseconds)
    {
    }

    public void RecordGeminiTimings(long totalMs, long httpMs, long parseMs)
    {
    }

    public void RecordValidationTimings(long totalMs, long watchedIdsMs, long resolutionMs)
    {
    }

    public void RecordValidationCatalogHit()
    {
    }

    public void RecordValidationProviderFallback()
    {
    }

    public void RecordValidationSearchFallback()
    {
    }

    public void RecordValidationDedupHit()
    {
    }

    public void RecordValidationUnsupportedMediaTypeRejection()
    {
    }

    public void RecordValidationResolutionFailureRejection()
    {
    }

    public void RecordValidationResponseDuplicateRejection()
    {
    }

    public void RecordValidationSessionDuplicateRejection()
    {
    }

    public void RecordValidationWatchedRejection()
    {
    }

    public void RecordValidationExcludedGenreRejection()
    {
    }

    public void RecordValidationRuntimeRejection()
    {
    }

    public void RecordValidationYearRejection()
    {
    }

    public void RecordTmdbResolutionCall()
    {
    }

    public void RecordSessionSaveMs(long milliseconds)
    {
    }

    public void RecordQuotaCommitMs(long milliseconds)
    {
    }

    public void SetOutcome(string outcome)
    {
    }

    public void SetResultCounts(int configuredSuggestionCount, int geminiSuggestionCount, int returnedCount)
    {
    }
}
