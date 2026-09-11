namespace MovieApp.Contracts.Users;

public sealed record UserProfileAuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    UserProfileResponse User);
