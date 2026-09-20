using Microsoft.Extensions.Logging;

namespace MovieApp.Application.Services.Identity;

internal static partial class SocialAuthServiceLogMessages
{
    [LoggerMessage(
        EventId = 2102,
        Level = LogLevel.Debug,
        Message = "SocialAuthPerf Provider={Provider} Outcome={Outcome} TotalMs={TotalMs} TokenVerificationMs={TokenVerificationMs} DbLookupMs={DbLookupMs} PersistenceMs={PersistenceMs} IssueTokenMs={IssueTokenMs}")]
    public static partial void LogPerf(
        ILogger logger,
        string provider,
        string outcome,
        long totalMs,
        long tokenVerificationMs,
        long dbLookupMs,
        long persistenceMs,
        long issueTokenMs);
}
