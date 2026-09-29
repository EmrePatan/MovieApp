namespace MovieApp.Contracts.Users;

public sealed record CreatePasswordRequest(
    string NewPassword,
    string Provider,
    string IdentityToken);
