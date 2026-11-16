namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialProfileResult(Guid Id, string UserName, string DisplayName, DateTime CreatedAt, string? AvatarUrl, bool IsPrivate, string FollowStatus, int FollowersCount, int FollowingCount);

public interface ISocialProfileService
{
    Task<SocialProfileResult> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
