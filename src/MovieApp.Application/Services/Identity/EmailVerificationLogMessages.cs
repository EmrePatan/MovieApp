using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

public static partial class EmailVerificationLogMessages
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Email verification delivery failed for user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogEmailVerificationDeliveryFailed(
        ILogger logger,
        Guid userId,
        string exceptionType);

    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Warning,
        Message = "Email verification delivery enqueue failed for token {TokenId} and user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogDeliveryEnqueueFailed(
        ILogger logger,
        Guid tokenId,
        Guid userId,
        string exceptionType);

    [LoggerMessage(
        EventId = 2103,
        Level = LogLevel.Information,
        Message = "Email verification delivery enqueued for token {TokenId}.")]
    public static partial void LogDeliveryEnqueued(
        ILogger logger,
        Guid tokenId);

    [LoggerMessage(
        EventId = 2104,
        Level = LogLevel.Information,
        Message = "Email verification delivery succeeded for token {TokenId} and user {UserId}.")]
    public static partial void LogDeliverySucceeded(
        ILogger logger,
        Guid tokenId,
        Guid userId);

    [LoggerMessage(
        EventId = 2105,
        Level = LogLevel.Warning,
        Message = "Email verification delivery attempt failed for token {TokenId} and user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogDeliveryAttemptFailed(
        ILogger logger,
        Guid tokenId,
        Guid userId,
        string exceptionType);

    [LoggerMessage(
        EventId = 2106,
        Level = LogLevel.Information,
        Message = "Email verification delivery skipped because token {TokenId} is no longer deliverable for user {UserId}.")]
    public static partial void LogDeliverySkippedNotDeliverable(
        ILogger logger,
        Guid tokenId,
        Guid userId);

    [LoggerMessage(
        EventId = 2107,
        Level = LogLevel.Warning,
        Message = "Email verification delivery target was not found for token {TokenId}.")]
    public static partial void LogDeliveryTargetMissing(
        ILogger logger,
        Guid tokenId);

    [LoggerMessage(
        EventId = 2108,
        Level = LogLevel.Warning,
        Message = "Background email verification delivery failed for token {TokenId}. Exception type: {ExceptionType}.")]
    public static partial void LogBackgroundDeliveryAttemptFailed(
        ILogger logger,
        Guid tokenId,
        string exceptionType);
}
