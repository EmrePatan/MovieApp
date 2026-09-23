using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Reviews;

internal static partial class ReviewTranslationLogMessages
{
    [LoggerMessage(
        EventId = 6301,
        Level = LogLevel.Information,
        Message = "Review translation requested for review {ReviewId} targeting {TargetLocale}.")]
    public static partial void LogTranslationRequested(
        ILogger logger,
        Guid reviewId,
        string targetLocale);

    [LoggerMessage(
        EventId = 6302,
        Level = LogLevel.Information,
        Message = "Review translation cache hit for review {ReviewId} targeting {TargetLocale}.")]
    public static partial void LogCacheHit(
        ILogger logger,
        Guid reviewId,
        string targetLocale);

    [LoggerMessage(
        EventId = 6303,
        Level = LogLevel.Information,
        Message = "Review translation cache miss for review {ReviewId} targeting {TargetLocale}.")]
    public static partial void LogCacheMiss(
        ILogger logger,
        Guid reviewId,
        string targetLocale);

    [LoggerMessage(
        EventId = 6304,
        Level = LogLevel.Information,
        Message = "Review translation succeeded for review {ReviewId} targeting {TargetLocale} with outcome {Outcome}.")]
    public static partial void LogTranslationSucceeded(
        ILogger logger,
        Guid reviewId,
        string targetLocale,
        string outcome);

    [LoggerMessage(
        EventId = 6305,
        Level = LogLevel.Warning,
        Message = "Review translation provider unavailable for review {ReviewId} targeting {TargetLocale}.")]
    public static partial void LogProviderUnavailable(
        ILogger logger,
        Guid reviewId,
        string targetLocale);

    [LoggerMessage(
        EventId = 6306,
        Level = LogLevel.Warning,
        Message = "Review translation provider quota exceeded for review {ReviewId} targeting {TargetLocale}.")]
    public static partial void LogProviderQuotaExceeded(
        ILogger logger,
        Guid reviewId,
        string targetLocale);
}
