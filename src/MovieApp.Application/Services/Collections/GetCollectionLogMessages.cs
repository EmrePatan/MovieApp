using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Collections;

internal static partial class GetCollectionLogMessages
{
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Warning,
        Message = "Collection provider failed for TMDB collection id {TmdbCollectionId}.")]
    internal static partial void LogProviderFailed(
        ILogger logger,
        int tmdbCollectionId,
        Exception exception);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Warning,
        Message = "Skipping malformed collection part for TMDB collection id {TmdbCollectionId} and TMDB movie id {TmdbMovieId}.")]
    internal static partial void LogMalformedPart(
        ILogger logger,
        int tmdbCollectionId,
        int tmdbMovieId);
}
