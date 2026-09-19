namespace MovieApp.IntegrationTests;

internal static class IntegrationTestJwtSettings
{
    internal const string SigningKey = "integration-test-signing-key-must-be-at-least-32-bytes";

    internal static Dictionary<string, string?> CreateConfiguration() =>
        new()
        {
            ["Authentication:Jwt:Issuer"] = "MovieApp",
            ["Authentication:Jwt:Audience"] = "MovieApp.Mobile",
            ["Authentication:Jwt:SigningKey"] = SigningKey,
            ["Authentication:Jwt:AccessTokenMinutes"] = "60",
            ["Authentication:RateLimit:RegisterPermitLimit"] = "1000",
            ["Authentication:RateLimit:RegisterWindowMinutes"] = "10",
            ["Authentication:RateLimit:LoginPermitLimit"] = "1000",
            ["Authentication:RateLimit:LoginWindowMinutes"] = "10",
            ["Authentication:RateLimit:ForgotPasswordPermitLimit"] = "1000",
            ["Authentication:RateLimit:ForgotPasswordWindowMinutes"] = "15",
            ["Authentication:RateLimit:ResetPasswordPermitLimit"] = "1000",
            ["Authentication:RateLimit:ResetPasswordWindowMinutes"] = "15",
            ["Authentication:RateLimit:VerifyEmailPermitLimit"] = "1000",
            ["Authentication:RateLimit:VerifyEmailWindowMinutes"] = "15",
            ["Authentication:RateLimit:ResendVerificationPermitLimit"] = "1000",
            ["Authentication:RateLimit:ResendVerificationWindowMinutes"] = "15"
        };
}
