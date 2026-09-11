namespace MovieApp.IntegrationTests.RatingsReviews;

internal static class RatingsReviewsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_ratings_reviews_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
