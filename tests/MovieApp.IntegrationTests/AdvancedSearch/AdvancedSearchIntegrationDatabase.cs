namespace MovieApp.IntegrationTests.AdvancedSearch;

internal static class AdvancedSearchIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_advanced_search_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
