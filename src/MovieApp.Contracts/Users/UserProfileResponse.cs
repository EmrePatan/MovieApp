namespace MovieApp.Contracts.Users;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    bool HasPassword,
    IReadOnlyList<string> LinkedProviders,
    string? PendingEmail = null,
    string? CustomAvatarUrl = null,
    string? ProviderAvatarUrl = null,
    string? EffectiveAvatarUrl = null,
    string AvatarKind = "initials");
