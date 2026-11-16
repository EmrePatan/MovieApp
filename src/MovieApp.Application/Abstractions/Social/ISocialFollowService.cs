namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialFollowResult(Guid UserId, string Status, bool IsFollowing);

public interface ISocialFollowService
{
    Task<SocialFollowResult> FollowAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task<SocialFollowResult> AcceptRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default);
    Task RejectRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default);
    Task BlockAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task UnblockAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}
