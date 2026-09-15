namespace MovieApp.IntegrationTests.Collections;

internal static class CollectionsIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_collections_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
