namespace MovieApp.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentUserResponse User);
