namespace MovieApp.Application.Models.Identity;

public sealed record CurrentUserResult(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt);
