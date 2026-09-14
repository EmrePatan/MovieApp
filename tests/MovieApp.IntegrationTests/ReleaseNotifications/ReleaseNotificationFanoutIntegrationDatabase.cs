namespace MovieApp.IntegrationTests.ReleaseNotifications;

internal static class ReleaseNotificationFanoutIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_release_notification_fanout_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
