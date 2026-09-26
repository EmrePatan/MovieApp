using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class ContentSearchTitleProviderEnrichmentLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Content search title enrichment skipped movie {MovieId} (TMDB {TmdbId} unavailable).")]
    public static partial void LogMovieSkippedUnavailable(ILogger logger, Guid movieId, int tmdbId);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Content search title enrichment failed for movie {MovieId} (TMDB {TmdbId}).")]
    public static partial void LogMovieFailed(ILogger logger, Guid movieId, int? tmdbId, Exception exception);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Content search title enrichment skipped TV {TvShowId} (TMDB {TmdbId} unavailable).")]
    public static partial void LogTvSkippedUnavailable(ILogger logger, Guid tvShowId, int tmdbId);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Error,
        Message = "Content search title enrichment failed for TV {TvShowId} (TMDB {TmdbId}).")]
    public static partial void LogTvFailed(ILogger logger, Guid tvShowId, int? tmdbId, Exception exception);
}
