namespace MovieApp.Domain.Entities;

public sealed class UserBlock
{
    public Guid Id { get; set; }

    public Guid BlockerId { get; set; }

    public Guid BlockedUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public static UserBlock Create(Guid blockerId, Guid blockedUserId, DateTime utcNow)
    {
        if (blockerId == Guid.Empty)
        {
            throw new ArgumentException("Blocker id is required.", nameof(blockerId));
        }

        if (blockedUserId == Guid.Empty)
        {
            throw new ArgumentException("Blocked user id is required.", nameof(blockedUserId));
        }

        if (blockerId == blockedUserId)
        {
            throw new InvalidOperationException("Users cannot block themselves.");
        }

        return new UserBlock
        {
            Id = Guid.NewGuid(),
            BlockerId = blockerId,
            BlockedUserId = blockedUserId,
            CreatedAt = utcNow
        };
    }
}
