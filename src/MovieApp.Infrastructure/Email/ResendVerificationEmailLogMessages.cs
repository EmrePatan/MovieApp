using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Email;

internal static partial class ResendVerificationEmailLogMessages
{
    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Information,
        Message = "Verification email accepted by Resend for token {TokenId}.")]
    internal static partial void LogDeliveryAccepted(ILogger logger, Guid tokenId);

    [LoggerMessage(
        EventId = 2202,
        Level = LogLevel.Warning,
        Message = "Verification email delivery failed for token {TokenId}. StatusCode={StatusCode} ExceptionType={ExceptionType} ProviderMessage={ProviderMessage}.")]
    internal static partial void LogDeliveryFailed(
        ILogger logger,
        Guid tokenId,
        int statusCode,
        string exceptionType,
        string? providerMessage);
}
