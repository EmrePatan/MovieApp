namespace MovieApp.IntegrationTests.People;

internal static class PeopleIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_people_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(PeopleIntegrationDatabase.DatabaseName);
}
