using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Identity;

internal static partial class GoogleIdTokenVerifierLogMessages
{
    [LoggerMessage(
        EventId = 7101,
        Level = LogLevel.Information,
        Message = "SocialAuthPerf GoogleTokenValidation TotalMs={TotalMs}")]
    public static partial void LogTokenValidation(ILogger logger, long totalMs);
}
