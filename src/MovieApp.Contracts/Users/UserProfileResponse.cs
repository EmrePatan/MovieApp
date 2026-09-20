namespace MovieApp.Contracts.Users;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    bool HasPassword,
    IReadOnlyList<string> LinkedProviders);
