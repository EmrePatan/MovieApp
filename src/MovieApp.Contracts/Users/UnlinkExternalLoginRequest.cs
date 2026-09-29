namespace MovieApp.Contracts.Users;

public sealed record UnlinkExternalLoginRequest(
    string? CurrentPassword,
    string? ReauthProvider,
    string? ReauthIdentityToken);
