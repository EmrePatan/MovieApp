using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Caching;

internal static partial class PersonalizedCacheRebuildLogMessages
{
    [LoggerMessage(
        EventId = 7110,
        Level = LogLevel.Information,
        Message = "Personalized cache rebuild finished: userId={UserId} durationMs={DurationMs}")]
    public static partial void LogRebuildFinished(ILogger logger, Guid userId, long durationMs);

    [LoggerMessage(
        EventId = 7111,
        Level = LogLevel.Warning,
        Message = "Personalized cache rebuild failed: userId={UserId}")]
    public static partial void LogRebuildFailed(ILogger logger, Guid userId, Exception exception);
}
