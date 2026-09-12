using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Movies;

internal static partial class SearchMoviesLogMessages
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Warning,
        Message = "Skipped movie search result due to external identifier persistence conflict. ExternalId={ExternalId} TmdbId={TmdbId}")]
    internal static partial void LogSkippedSearchResultPersistenceConflict(
        ILogger logger,
        string externalId,
        int? tmdbId);
}
