using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Discovery;

internal static partial class OnTvThisWeekLogMessages
{
    [LoggerMessage(
        EventId = 4402,
        Level = LogLevel.Warning,
        Message = "On TV this week provider failed for page {Page}.")]
    public static partial void LogProviderFailed(ILogger logger, int page, Exception exception);
}
