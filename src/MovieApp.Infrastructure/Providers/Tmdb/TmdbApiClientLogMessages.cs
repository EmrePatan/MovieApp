using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Providers.Tmdb;

internal static partial class TmdbApiClientLogMessages
{
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Warning,
        Message = "TMDB request failed: path={RelativePath} statusCode={StatusCode} attempt={Attempt}")]
    internal static partial void LogRequestFailed(
        ILogger logger,
        string relativePath,
        int statusCode,
        int attempt);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Debug,
        Message = "TMDB request retry scheduled: path={RelativePath} statusCode={StatusCode} attempt={Attempt} delayMs={DelayMs}")]
    internal static partial void LogRequestRetryScheduled(
        ILogger logger,
        string relativePath,
        int statusCode,
        int attempt,
        long delayMs);

    [LoggerMessage(
        EventId = 7003,
        Level = LogLevel.Warning,
        Message = "TMDB request transport failure: path={RelativePath} attempt={Attempt}")]
    internal static partial void LogTransportFailure(
        ILogger logger,
        string relativePath,
        int attempt,
        Exception exception);
}