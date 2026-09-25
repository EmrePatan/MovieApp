using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static partial class RecommendationRepositoryLogMessages
{
    [LoggerMessage(
        EventId = 7211,
        Level = LogLevel.Information,
        Message = "RecHomePerf CandidateFetch DbRoundTrips={DbRoundTrips} MovieFetchMs={MovieFetchMs} TvFetchMs={TvFetchMs} DbTotalMs={DbTotalMs} MovieIdCount={MovieIdCount} TvIdCount={TvIdCount} CandidateCount={CandidateCount} ContentType={ContentType}")]
    public static partial void LogCandidateFetch(
        ILogger logger,
        int dbRoundTrips,
        long movieFetchMs,
        long tvFetchMs,
        long dbTotalMs,
        int movieIdCount,
        int tvIdCount,
        int candidateCount,
        string contentType);

    [LoggerMessage(
        EventId = 7212,
        Level = LogLevel.Information,
        Message = "RecHomePerf MovieCandidateFetch TotalMs={TotalMs} IdSelectionMs={IdSelectionMs} HydrationMs={HydrationMs} KeywordMs={KeywordMs} DbRoundTrips={DbRoundTrips} CandidateIdCount={CandidateIdCount} ResultCount={ResultCount}")]
    public static partial void LogMovieCandidateFetch(
        ILogger logger,
        long totalMs,
        long idSelectionMs,
        long hydrationMs,
        long keywordMs,
        int dbRoundTrips,
        int candidateIdCount,
        int resultCount);
}
