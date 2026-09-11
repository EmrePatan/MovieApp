namespace MovieApp.IntegrationTests.Recommendations;

internal static class RecommendationsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_recommendations_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
