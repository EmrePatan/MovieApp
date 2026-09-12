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

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Unified search movie provider ingestion failed for query {Query} page {Page}.")]
    public static partial void LogMovieIngestionFailed(
        ILogger logger,
        string query,
        int page,
        Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Unified search TV provider ingestion failed for query {Query} page {Page}.")]
    public static partial void LogTvIngestionFailed(
        ILogger logger,
        string query,
        int page,
        Exception exception);
}
