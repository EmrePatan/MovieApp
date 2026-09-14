namespace MovieApp.IntegrationTests.PushNotifications;

internal static class PushNotificationDeliveryIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_push_notification_delivery_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
