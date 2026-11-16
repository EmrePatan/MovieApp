namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialProfileResult(Guid Id, string UserName, string DisplayName, DateTime CreatedAt, string? AvatarUrl, bool IsPrivate, string FollowStatus, int FollowersCount, int FollowingCount);

public sealed record SocialUserSummaryResult(Guid Id, string UserName, string DisplayName, string? AvatarUrl, string FollowStatus);

public interface ISocialProfileService
{
    Task<SocialProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> SearchUsersAsync(string query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowersAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialUserSummaryResult>> GetFollowingAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
}
