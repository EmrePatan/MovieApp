namespace MovieApp.Contracts.Users;

public sealed record DeleteAccountRequest(
    string? CurrentPassword = null,
    string? Provider = null,
    string? IdentityToken = null);
