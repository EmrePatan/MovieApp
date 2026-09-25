namespace MovieApp.Application.Models.Identity;

public sealed record AuthenticationResult(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentUserResult User,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt);
