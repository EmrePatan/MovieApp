using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Email;

internal static partial class ResendAccountEmailChangedNotificationLogMessages
{
    [LoggerMessage(EventId = 4301, Level = LogLevel.Warning, Message = "Account email changed notification skipped; Resend is not configured.")]
    public static partial void LogSkippedNotConfigured(ILogger logger);

    [LoggerMessage(EventId = 4302, Level = LogLevel.Warning, Message = "Account email changed notification delivery failed ({Detail}).")]
    public static partial void LogDeliveryFailed(ILogger logger, object detail);
}
