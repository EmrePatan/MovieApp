namespace MovieApp.IntegrationTests.ProductMetrics;

internal static class ProductMetricsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_product_metrics_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
