using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Identity;

internal static partial class AppleJwksProviderLogMessages
{
    [LoggerMessage(
        EventId = 7102,
        Level = LogLevel.Information,
        Message = "SocialAuthPerf AppleJwks Cache={CacheResult} TotalMs={TotalMs}")]
    public static partial void LogFetch(ILogger logger, string cacheResult, long totalMs);
}
