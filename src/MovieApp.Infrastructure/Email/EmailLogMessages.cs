using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Email;

internal static partial class EmailLogMessages
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Password reset email queued for {Email}. Reset link path configured.")]
    public static partial void PasswordResetQueued(ILogger logger, string email);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Failed to send password reset email to {Email}.")]
    public static partial void PasswordResetSendFailed(ILogger logger, Exception exception, string email);
}
