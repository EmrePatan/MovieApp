namespace MovieApp.Contracts.Users;

public sealed record ChangeEmailRequest(
    string Email,
    string? CurrentPassword = null,
    string? ReauthProvider = null,
    string? ReauthIdentityToken = null);
