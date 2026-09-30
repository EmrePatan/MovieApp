using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

internal static partial class UserAvatarServiceLogMessages
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Failed to delete user avatar object from storage. ExceptionType={ExceptionType}")]
    public static partial void LogCleanupFailed(ILogger logger, string exceptionType);
}
