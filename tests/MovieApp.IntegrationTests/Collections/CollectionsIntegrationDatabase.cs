namespace MovieApp.IntegrationTests.Collections;

internal static class CollectionsIntegrationDatabase
{
    internal static string GetConnectionString() =>
        Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5432;Database=movieapp_collections_tests;Username=postgres;Password=postgres";
}
