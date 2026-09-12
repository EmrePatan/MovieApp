namespace MovieApp.IntegrationTests.Auth;

internal static class AuthIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_auth_integration_tests";

    internal const string RateLimitDatabaseName = "movieapp_auth_rate_limit_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);

    internal static string GetRateLimitConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(RateLimitDatabaseName);
}
