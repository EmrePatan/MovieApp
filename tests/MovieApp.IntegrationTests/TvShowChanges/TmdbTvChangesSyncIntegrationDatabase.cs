namespace MovieApp.IntegrationTests.TvShowChanges;

internal static class TmdbTvChangesSyncIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_tmdb_tv_changes_sync_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
