using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Home;

internal static partial class HotThisWeekLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Hot This Week trending provider failed.")]
    public static partial void LogTrendingProviderFailed(ILogger logger, Exception exception);
}
