namespace MovieApp.Application.Models.Identity;

public sealed record UserProfileResult(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    bool HasPassword,
    IReadOnlyList<string> LinkedProviders);
