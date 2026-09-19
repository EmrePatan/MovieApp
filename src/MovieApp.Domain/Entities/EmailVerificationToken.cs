namespace MovieApp.Domain.Entities;

public sealed class EmailVerificationToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive(DateTime utcNow) =>
        UsedAtUtc is null && ExpiresAtUtc > utcNow;
}
