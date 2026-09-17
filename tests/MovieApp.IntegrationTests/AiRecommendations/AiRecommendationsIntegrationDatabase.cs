namespace MovieApp.IntegrationTests.AiRecommendations;

internal static class AiRecommendationsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_ai_recommendations_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
