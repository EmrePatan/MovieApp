using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

internal static partial class ForgotPasswordLogMessages
{
    [LoggerMessage(
        EventId = 2101,
        Level = LogLevel.Warning,
        Message = "Password reset email delivery failed for user {UserId}. ExceptionType={ExceptionType}")]
    internal static partial void LogPasswordResetEmailDeliveryFailed(
        ILogger logger,
        Guid userId,
        string exceptionType);
}
