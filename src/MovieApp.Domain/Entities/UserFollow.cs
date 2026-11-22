using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class UserFollow
{
    public Guid Id { get; set; }

    public Guid FollowerId { get; set; }

    public Guid FollowedUserId { get; set; }

    public UserFollowStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }

    public static UserFollow Create(
        Guid followerId,
        Guid followedUserId,
        bool followedUserIsPrivate,
        DateTime utcNow)
    {
        if (followerId == Guid.Empty)
        {
            throw new ArgumentException("Follower id is required.", nameof(followerId));
        }

        if (followedUserId == Guid.Empty)
        {
            throw new ArgumentException("Followed user id is required.", nameof(followedUserId));
        }

        if (followerId == followedUserId)
        {
            throw new InvalidOperationException("Users cannot follow themselves.");
        }

        return new UserFollow
        {
            Id = Guid.NewGuid(),
            FollowerId = followerId,
            FollowedUserId = followedUserId,
            Status = followedUserIsPrivate ? UserFollowStatus.Pending : UserFollowStatus.Accepted,
            CreatedAt = utcNow,
            RespondedAt = followedUserIsPrivate ? null : utcNow
        };
    }

    public void Accept(DateTime utcNow)
    {
        if (Status != UserFollowStatus.Pending)
        {
            throw new InvalidOperationException("Only pending follow requests can be accepted.");
        }

        Status = UserFollowStatus.Accepted;
        RespondedAt = utcNow;
    }
}
