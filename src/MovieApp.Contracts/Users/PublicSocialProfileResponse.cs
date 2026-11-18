namespace MovieApp.Contracts.Users;

public sealed record PublicSocialProfileResponse(
    Guid Id, string UserName, string DisplayName, DateTime CreatedAt, string? AvatarUrl,
    bool IsPrivate, string FollowStatus, int FollowersCount, int FollowingCount);
