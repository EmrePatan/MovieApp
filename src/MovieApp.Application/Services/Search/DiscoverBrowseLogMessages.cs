using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Search;

internal static partial class DiscoverBrowseLogMessages
{
    [LoggerMessage(
        EventId = 4301,
        Level = LogLevel.Warning,
        Message = "Movie discover provider failed for page {Page}.")]
    public static partial void LogMovieDiscoverFailed(ILogger logger, int page, Exception exception);

    [LoggerMessage(
        EventId = 4302,
        Level = LogLevel.Warning,
        Message = "TV discover provider failed for page {Page}.")]
    public static partial void LogTvDiscoverFailed(ILogger logger, int page, Exception exception);
}
