namespace MovieApp.Application.Abstractions.Social;

public sealed record SocialFollowResult(Guid UserId, string Status, bool IsFollowing);

public sealed record SocialFollowRequestResult(Guid UserId, string UserName, string DisplayName, DateTime RequestedAt);

public sealed record SocialBlockedUserResult(Guid UserId, string UserName, string DisplayName, DateTime BlockedAt);

public interface ISocialFollowService
{
    Task<SocialFollowResult> FollowAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task UnfollowAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialFollowRequestResult>> GetPendingRequestsAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<SocialFollowResult> AcceptRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default);
    Task RejectRequestAsync(Guid followerUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SocialBlockedUserResult>> GetBlockedUsersAsync(CancellationToken cancellationToken = default);
    Task BlockAsync(Guid targetUserId, CancellationToken cancellationToken = default);
    Task UnblockAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}
