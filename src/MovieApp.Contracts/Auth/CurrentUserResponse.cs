namespace MovieApp.Contracts.Auth;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt);
