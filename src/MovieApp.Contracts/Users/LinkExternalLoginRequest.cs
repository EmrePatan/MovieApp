namespace MovieApp.Contracts.Users;

public sealed record LinkExternalLoginRequest(
    string? CurrentPassword,
    string? ReauthProvider,
    string? ReauthIdentityToken,
    string TargetProvider,
    string TargetIdentityToken);
