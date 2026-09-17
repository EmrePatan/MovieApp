using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Persistence.Repositories;

internal static partial class RecommendationRepositoryLogMessages
{
    [LoggerMessage(
        EventId = 7211,
        Level = LogLevel.Information,
        Message = "RecHomePerf CandidateFetch MovieIdsDbMs={MovieIdsDbMs} MovieProjectionsDbMs={MovieProjectionsDbMs} TvIdsDbMs={TvIdsDbMs} TvProjectionsDbMs={TvProjectionsDbMs} MovieKeywordsDbMs={MovieKeywordsDbMs} TvKeywordsDbMs={TvKeywordsDbMs} DbTotalMs={DbTotalMs} MovieIdCount={MovieIdCount} TvIdCount={TvIdCount} CandidateCount={CandidateCount} ContentType={ContentType}")]
    public static partial void LogCandidateFetch(
        ILogger logger,
        long movieIdsDbMs,
        long movieProjectionsDbMs,
        long tvIdsDbMs,
        long tvProjectionsDbMs,
        long movieKeywordsDbMs,
        long tvKeywordsDbMs,
        long dbTotalMs,
        int movieIdCount,
        int tvIdCount,
        int candidateCount,
        string contentType);
}
