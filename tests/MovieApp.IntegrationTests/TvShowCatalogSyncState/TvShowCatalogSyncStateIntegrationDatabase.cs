namespace MovieApp.IntegrationTests.TvShowCatalogSyncState;

internal static class TvShowCatalogSyncStateIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_tvshow_catalog_sync_state_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
