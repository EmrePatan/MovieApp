namespace MovieApp.Domain.Entities;

public sealed class WatchedEpisode
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid EpisodeId { get; set; }

    public DateTime WatchedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Episode Episode { get; set; } = null!;

    public static WatchedEpisode Create(Guid userId, Guid episodeId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (episodeId == Guid.Empty)
        {
            throw new ArgumentException("Episode id is required.", nameof(episodeId));
        }

        return new WatchedEpisode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EpisodeId = episodeId,
            WatchedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdateWatchedAt(DateTime utcNow)
    {
        WatchedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
