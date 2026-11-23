namespace MovieApp.Contracts.Users;

public sealed record PublicSocialProfileResponse(
    Guid Id,
    string UserName,
    string DisplayName,
    DateTime CreatedAt,
    string? AvatarUrl,
    bool IsPrivate,
    string FollowStatus,
    int FollowersCount,
    int FollowingCount,
    string? CoverPosterPath,
    string? CoverImageUrl,
    string? Bio = null,
    ProfileSocialLinksDto? SocialLinks = null,
    bool FollowsViewer = false,
    int? AchievementPoints = null,
    IReadOnlyList<PublicRecentAchievementResponse>? RecentAchievements = null);

/// <summary>One recently unlocked achievement tier, shown in the member's level sheet.</summary>
public sealed record PublicRecentAchievementResponse(
    string Id,
    string Category,
    int Tier,
    int TargetValue,
    DateTime AchievedAt);
