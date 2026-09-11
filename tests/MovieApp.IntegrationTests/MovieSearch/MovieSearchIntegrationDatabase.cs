namespace MovieApp.IntegrationTests.MovieSearch;

internal static class MovieSearchIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_search_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(MovieSearchIntegrationDatabase.DatabaseName);
}
