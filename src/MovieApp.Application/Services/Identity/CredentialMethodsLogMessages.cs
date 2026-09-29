using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

public static partial class CredentialMethodsLogMessages
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Information,
        Message = "External login linked for user {UserId}.")]
    public static partial void LogProviderLinked(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4102,
        Level = LogLevel.Information,
        Message = "External login unlinked for user {UserId}.")]
    public static partial void LogProviderUnlinked(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4103,
        Level = LogLevel.Information,
        Message = "Password created for user {UserId}.")]
    public static partial void LogPasswordCreated(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Information,
        Message = "Email change requested for user {UserId}.")]
    public static partial void LogEmailChangeRequested(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 4105,
        Level = LogLevel.Information,
        Message = "Pending email change verification resent for user {UserId}.")]
    public static partial void LogPendingEmailChangeResent(ILogger logger, Guid userId);
}
