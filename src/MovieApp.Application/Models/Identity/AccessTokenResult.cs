namespace MovieApp.Application.Models.Identity;

public sealed record AccessTokenResult(string AccessToken, DateTime ExpiresAt);
