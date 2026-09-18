using Microsoft.Extensions.Logging;

namespace MovieApp.Infrastructure.Identity;

internal static partial class AppleIdTokenVerifierLogMessages
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Apple identity token validation failed with {FailureType}. Configured audiences: {ConfiguredAudiences}. Token issuer: {TokenIssuer}. Token audiences: {TokenAudiences}. Token expires: {TokenExpiresAtUtc}.")]
    public static partial void LogValidationFailed(
        ILogger logger,
        string failureType,
        string configuredAudiences,
        string tokenIssuer,
        string tokenAudiences,
        string tokenExpiresAtUtc);
}
