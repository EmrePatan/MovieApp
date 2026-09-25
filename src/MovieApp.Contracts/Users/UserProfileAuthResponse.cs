namespace MovieApp.Contracts.Users;

public sealed record UserProfileAuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserProfileResponse User);
