namespace MovieApp.IntegrationTests.FavoritesWatchlists;

internal static class FavoritesWatchlistsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_favorites_watchlists_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
