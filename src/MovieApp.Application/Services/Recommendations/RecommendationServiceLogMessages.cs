using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Recommendations;

internal static partial class RecommendationServiceLogMessages
{
    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Debug,
        Message = "RecHomePerf Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs}")]
    public static partial void LogCacheHit(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs);

    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Debug,
        Message = "RecHomePerf Cache={CacheResult} TotalMs={TotalMs} CacheLookupMs={CacheLookupMs} UserContextMs={UserContextMs} PersonalizedSectionMs={PersonalizedSectionMs} BecauseYouWatchedMs={BecauseYouWatchedMs} CacheWriteMs={CacheWriteMs} MeaningfulInteractionCount={MeaningfulInteractionCount} SectionCount={SectionCount}")]
    public static partial void LogCacheMiss(
        ILogger logger,
        string cacheResult,
        long totalMs,
        long cacheLookupMs,
        long userContextMs,
        long personalizedSectionMs,
        long becauseYouWatchedMs,
        long cacheWriteMs,
        int meaningfulInteractionCount,
        int sectionCount);

    [LoggerMessage(
        EventId = 7103,
        Level = LogLevel.Debug,
        Message = "RecHomePerf Personalized PreferenceBuildCpuMs={PreferenceBuildCpuMs} CandidateFetchMs={CandidateFetchMs} ScoringCpuMs={ScoringCpuMs} DiversityCpuMs={DiversityCpuMs} PaginationCpuMs={PaginationCpuMs} CandidateCount={CandidateCount} ScoredCount={ScoredCount}")]
    public static partial void LogPersonalizedBuild(
        ILogger logger,
        long preferenceBuildCpuMs,
        long candidateFetchMs,
        long scoringCpuMs,
        long diversityCpuMs,
        long paginationCpuMs,
        int candidateCount,
        int scoredCount);

    [LoggerMessage(
        EventId = 7104,
        Level = LogLevel.Debug,
        Message = "RecHomePerf BecauseYouWatched MovieAggregateMs={MovieAggregateMs} TvAggregateMs={TvAggregateMs} WatchedSourceCount={WatchedSourceCount} ResultCount={ResultCount}")]
    public static partial void LogBecauseYouWatched(
        ILogger logger,
        long movieAggregateMs,
        long tvAggregateMs,
        int watchedSourceCount,
        int resultCount);

    [LoggerMessage(
        EventId = 7105,
        Level = LogLevel.Debug,
        Message = "RecHomePerf SimilarityAggregate ContentType={ContentType} SourceProfilesMs={SourceProfilesMs} CandidateIdsMs={CandidateIdsMs} CandidateProfilesMs={CandidateProfilesMs} CpuRankMs={CpuRankMs} SourceCount={SourceCount} UniqueCandidateCount={UniqueCandidateCount}")]
    public static partial void LogSimilarityAggregate(
        ILogger logger,
        string contentType,
        long sourceProfilesMs,
        long candidateIdsMs,
        long candidateProfilesMs,
        long cpuRankMs,
        int sourceCount,
        int uniqueCandidateCount);
}
