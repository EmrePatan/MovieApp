namespace MovieApp.Contracts.Auth;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    CurrentUserResponse User);
