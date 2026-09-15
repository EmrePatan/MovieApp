using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Discovery;

internal static partial class NowInTheatersLogMessages
{
    [LoggerMessage(
        EventId = 4401,
        Level = LogLevel.Warning,
        Message = "Now in theaters provider failed for region {ReleaseRegion} page {Page}.")]
    public static partial void LogProviderFailed(
        ILogger logger,
        string releaseRegion,
        int page,
        Exception exception);
}
