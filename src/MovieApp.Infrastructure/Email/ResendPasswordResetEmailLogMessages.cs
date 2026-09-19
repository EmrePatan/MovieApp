using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Email;

internal static partial class ResendPasswordResetEmailLogMessages
{
    [LoggerMessage(
        EventId = 2211,
        Level = LogLevel.Information,
        Message = "Resend accepted password reset email delivery for token {TokenId}.")]
    internal static partial void LogDeliveryAccepted(ILogger logger, Guid tokenId);

    [LoggerMessage(
        EventId = 2212,
        Level = LogLevel.Warning,
        Message = "Resend password reset email delivery failed for token {TokenId}. StatusCode={StatusCode} ExceptionType={ExceptionType}.")]
    internal static partial void LogDeliveryFailed(
        ILogger logger,
        Guid tokenId,
        int statusCode,
        string exceptionType);
}
