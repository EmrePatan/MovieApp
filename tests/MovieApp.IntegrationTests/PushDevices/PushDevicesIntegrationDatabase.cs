namespace MovieApp.IntegrationTests.PushDevices;

internal static class PushDevicesIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_push_devices_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
