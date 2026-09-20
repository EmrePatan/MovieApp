using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static partial class RecommendationRepositoryLogMessages
{
    [LoggerMessage(
        EventId = 7211,
        Level = LogLevel.Debug,
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
}
