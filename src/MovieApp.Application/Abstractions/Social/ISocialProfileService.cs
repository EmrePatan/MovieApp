namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialProfileResult(Guid Id, string UserName, string DisplayName, DateTime CreatedAt, string? AvatarUrl, bool IsPrivate, string FollowStatus, int FollowersCount, int FollowingCount);

public sealed record SocialUserSummaryResult(Guid Id, string UserName, string DisplayName, string? AvatarUrl, string FollowStatus);\n\npublic sealed record SocialProfileMilestoneResult(string Id, string Title, string Description, DateTime? AchievedAt);\n\npublic sealed record SocialProfileStatisticsResult(\n    int MoviesWatched, int EpisodesWatched, int RatingsCount, int ReviewsCount, int FavoritesCount,\n    IReadOnlyList<SocialProfileMilestoneResult> Milestones);

public interface ISocialProfileService
{
    Task<SocialProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);\n    Task<SocialProfileStatisticsResult> GetStatisticsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> SearchUsersAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowersAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowingAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
