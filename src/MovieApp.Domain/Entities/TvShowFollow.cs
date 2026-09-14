namespace MovieApp.Domain.Entities;

public sealed class TvShowFollow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid TvShowId { get; set; }

    public bool NotifyNewSeasons { get; set; } = true;

    public bool NotifyNewEpisodes { get; set; } = true;

    public DateTime? NotifyFromUtc { get; set; }

    public DateTime? BaselineEstablishedAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public TvShow TvShow { get; set; } = null!;

    public static TvShowFollow Create(
        Guid userId,
        Guid tvShowId,
        bool notifyNewSeasons,
        bool notifyNewEpisodes,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        return new TvShowFollow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TvShowId = tvShowId,
            NotifyNewSeasons = notifyNewSeasons,
            NotifyNewEpisodes = notifyNewEpisodes,
            NotifyFromUtc = null,
            BaselineEstablishedAtUtc = null,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdatePreferences(bool notifyNewSeasons, bool notifyNewEpisodes, DateTime utcNow)
    {
        NotifyNewSeasons = notifyNewSeasons;
        NotifyNewEpisodes = notifyNewEpisodes;
        UpdatedAt = utcNow;
    }

    public void SetNotifyFromUtc(DateTime notifyFromUtc, DateTime utcNow)
    {
        if (NotifyFromUtc.HasValue)
        {
            return;
        }

        NotifyFromUtc = notifyFromUtc;
        UpdatedAt = utcNow;
    }

    public void EstablishBaseline(DateTime utcNow)
    {
        if (BaselineEstablishedAtUtc.HasValue)
        {
            return;
        }

        if (!NotifyFromUtc.HasValue)
        {
            throw new InvalidOperationException("NotifyFromUtc must be set before establishing baseline.");
        }

        BaselineEstablishedAtUtc = utcNow;
        UpdatedAt = utcNow;
    }

    public bool IsBaselineEstablished => BaselineEstablishedAtUtc is not null;
}
