namespace MovieApp.Application.Services.Identity;

internal static class SignInMethodPolicy
{
    internal static int CountUsableSignInMethods(bool hasPassword, int linkedProviderCount) =>
        (hasPassword ? 1 : 0) + linkedProviderCount;

    internal static bool CanUnlinkProvider(bool hasPassword, int linkedProviderCount) =>
        CountUsableSignInMethods(hasPassword, linkedProviderCount) > 1 &&
        linkedProviderCount > 0;

    internal static bool HasRealVerifiedEmailForPasswordLogin(string email, bool isEmailVerified) =>
        isEmailVerified &&
        !string.IsNullOrWhiteSpace(email) &&
        !email.EndsWith("@external.movieapp.local", StringComparison.OrdinalIgnoreCase);
}
