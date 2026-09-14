namespace MovieApp.IntegrationTests.HotRelease;

internal static class HotReleaseIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_hot_release_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
