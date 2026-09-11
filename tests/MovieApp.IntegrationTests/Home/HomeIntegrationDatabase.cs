namespace MovieApp.IntegrationTests.Home;

internal static class HomeIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_home_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
