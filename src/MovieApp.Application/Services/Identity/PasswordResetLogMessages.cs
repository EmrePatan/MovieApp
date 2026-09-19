using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

public static partial class PasswordResetLogMessages
{
    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Warning,
        Message = "Password reset delivery enqueue failed for token {TokenId} and user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogDeliveryEnqueueFailed(
        ILogger logger,
        Guid tokenId,
        Guid userId,
        string exceptionType);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Information,
        Message = "Password reset delivery enqueued for token {TokenId}.")]
    public static partial void LogDeliveryEnqueued(
        ILogger logger,
        Guid tokenId);

    [LoggerMessage(
        EventId = 2204,
        Level = LogLevel.Information,
        Message = "Password reset delivery succeeded for token {TokenId} and user {UserId}.")]
    public static partial void LogDeliverySucceeded(
        ILogger logger,
        Guid tokenId,
        Guid userId);

    [LoggerMessage(
        EventId = 2205,
        Level = LogLevel.Warning,
        Message = "Password reset delivery attempt failed for token {TokenId} and user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogDeliveryAttemptFailed(
        ILogger logger,
        Guid tokenId,
        Guid userId,
        string exceptionType);

    [LoggerMessage(
        EventId = 2206,
        Level = LogLevel.Information,
        Message = "Password reset delivery skipped because token {TokenId} is no longer deliverable for user {UserId}.")]
    public static partial void LogDeliverySkippedNotDeliverable(
        ILogger logger,
        Guid tokenId,
        Guid userId);

    [LoggerMessage(
        EventId = 2207,
        Level = LogLevel.Warning,
        Message = "Password reset delivery target was not found for token {TokenId}.")]
    public static partial void LogDeliveryTargetMissing(
        ILogger logger,
        Guid tokenId);

    [LoggerMessage(
        EventId = 2208,
        Level = LogLevel.Warning,
        Message = "Background password reset delivery failed for token {TokenId}. Exception type: {ExceptionType}.")]
    public static partial void LogBackgroundDeliveryAttemptFailed(
        ILogger logger,
        Guid tokenId,
        string exceptionType);
}
