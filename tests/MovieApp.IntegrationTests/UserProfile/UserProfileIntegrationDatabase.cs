namespace MovieApp.IntegrationTests.UserProfile;

internal static class UserProfileIntegrationDatabase
{
    internal const string DatabaseName = "movieapp_user_profile_integration_tests";

    internal static string GetConnectionString() =>
        IntegrationTestDatabase.GetConnectionString(DatabaseName);
}
