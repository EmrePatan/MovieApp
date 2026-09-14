namespace MovieApp.IntegrationTests.TvShowFollows;

internal static class TvShowFollowsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_tvshow_follows_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
