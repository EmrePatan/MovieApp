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

    void RecordValidationTimings(long totalMs, long watchedIdsMs);

    void RecordTmdbResolutionCall();

    void RecordSessionSaveMs(long milliseconds);

    void RecordQuotaCommitMs(long milliseconds);

    void SetOutcome(string outcome);

    void SetResultCounts(int suggestionCount, int returnedCount);
}
