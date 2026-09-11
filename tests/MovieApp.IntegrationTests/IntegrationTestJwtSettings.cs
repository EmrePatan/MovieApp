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
            ["Authentication:Jwt:AccessTokenMinutes"] = "60"
        };
}
