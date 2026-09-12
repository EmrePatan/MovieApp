using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class UnifiedSearchProviderIngestionLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Skipped unified search movie ingestion for external ID {ExternalId} (TMDB {TmdbId}) due to persistence conflict.")]
    public static partial void LogSkippedMoviePersistenceConflict(
        ILogger logger,
        string externalId,
        int? tmdbId);
}
