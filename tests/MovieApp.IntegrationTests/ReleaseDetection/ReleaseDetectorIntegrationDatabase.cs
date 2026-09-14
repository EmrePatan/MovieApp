namespace MovieApp.IntegrationTests.ReleaseDetection;

internal static class ReleaseDetectorIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_release_detector_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
