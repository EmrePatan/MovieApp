namespace MovieApp.IntegrationTests.TvShowSearch;

internal static class TvShowSearchIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_tvshow_search_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(TvShowSearchIntegrationDatabase.DatabaseName);
}
