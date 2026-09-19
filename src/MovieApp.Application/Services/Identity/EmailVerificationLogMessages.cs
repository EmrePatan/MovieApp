using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

internal static partial class EmailVerificationLogMessages
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Email verification delivery failed for user {UserId}. Exception type: {ExceptionType}.")]
    public static partial void LogEmailVerificationDeliveryFailed(
        ILogger logger,
        Guid userId,
        string exceptionType);
}
