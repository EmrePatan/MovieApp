namespace MovieApp.IntegrationTests.Auth;

internal static class AuthIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_auth_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(AuthIntegrationDatabase.DatabaseName);
}
