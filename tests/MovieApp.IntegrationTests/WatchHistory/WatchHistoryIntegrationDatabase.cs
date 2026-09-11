namespace MovieApp.IntegrationTests.WatchHistory;

internal static class WatchHistoryIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_watch_history_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
