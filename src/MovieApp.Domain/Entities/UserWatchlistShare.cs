namespace MovieApp.Domain.Entities;

public sealed class UserWatchlistShare
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid WatchlistId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? DeactivatedAtUtc { get; set; }

    public DateTime? RotatedAtUtc { get; set; }

    public User User { get; set; } = null!;

    public Watchlist Watchlist { get; set; } = null!;
}
