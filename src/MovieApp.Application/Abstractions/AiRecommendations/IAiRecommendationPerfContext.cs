using MovieApp.Application.Models.AiRecommendations;

namespace MovieApp.Application.Abstractions.AiRecommendations;

public interface IAiRecommendationPerfContext
{
    AiRecommendationPerfMetrics Metrics { get; }

    void RecordQuotaReserveMs(long milliseconds);

    void RecordTasteProfileCacheHit(long totalMs, long cacheLookupMs);

    void RecordTasteProfileCacheMiss(
        long totalMs,
        long cacheLookupMs,
        long buildCpuMs,
        long cacheWriteMs);

    void RecordTasteProfileQuery(string queryName, long milliseconds);

    void RecordSessionLoadMs(long milliseconds);

    void RecordGeminiTimings(long totalMs, long httpMs, long parseMs);

    void RecordValidationTimings(long totalMs, long watchedIdsMs, long resolutionMs);

    void RecordValidationCatalogHit();

    void RecordValidationProviderFallback();

    void RecordValidationSearchFallback();

    void RecordValidationDedupHit();

    void RecordValidationUnsupportedMediaTypeRejection();

    void RecordValidationResolutionFailureRejection();

    void RecordValidationResponseDuplicateRejection();

    void RecordValidationSessionDuplicateRejection();

    void RecordValidationWatchedRejection();

    void RecordValidationExcludedGenreRejection();

    void RecordValidationRuntimeRejection();

    void RecordValidationYearRejection();

    void RecordTmdbResolutionCall();

    void RecordSessionSaveMs(long milliseconds);

    void RecordQuotaCommitMs(long milliseconds);

    void SetOutcome(string outcome);

    void SetResultCounts(int configuredSuggestionCount, int geminiSuggestionCount, int returnedCount);
}
