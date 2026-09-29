using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

public static partial class VerifyEmailLogMessages
{
    [LoggerMessage(
        EventId = 4201,
        Level = LogLevel.Warning,
        Message = "Account email changed notification delivery failed ({ExceptionType}).")]
    public static partial void LogAccountEmailChangedNotificationFailed(ILogger logger, string exceptionType);
}
