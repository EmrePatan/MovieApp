using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class CatalogFollow
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public CatalogContentType ContentType { get; set; }

    public Guid ContentId { get; set; }

    public bool NotifyMovieRelease { get; set; }

    public bool NotifyNewSeasons { get; set; } = true;

    public bool NotifyNewEpisodes { get; set; } = true;

    public DateTime? NotifyFromUtc { get; set; }

    public DateTime? BaselineEstablishedAtUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public bool IsBaselineEstablished => BaselineEstablishedAtUtc is not null;

    public Guid TvShowId => ContentType == CatalogContentType.Tv
        ? ContentId
        : throw new InvalidOperationException("Catalog follow is not a TV show follow.");

    public Guid MovieId => ContentType == CatalogContentType.Movie
        ? ContentId
        : throw new InvalidOperationException("Catalog follow is not a movie follow.");

    public static CatalogFollow CreateTvFollow(
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

        return new CatalogFollow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContentType = CatalogContentType.Tv,
            ContentId = tvShowId,
            NotifyMovieRelease = false,
            NotifyNewSeasons = notifyNewSeasons,
            NotifyNewEpisodes = notifyNewEpisodes,
            NotifyFromUtc = null,
            BaselineEstablishedAtUtc = null,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static CatalogFollow CreateMovieFollow(Guid userId, Guid movieId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new CatalogFollow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContentType = CatalogContentType.Movie,
            ContentId = movieId,
            NotifyMovieRelease = true,
            NotifyNewSeasons = false,
            NotifyNewEpisodes = false,
            NotifyFromUtc = null,
            BaselineEstablishedAtUtc = null,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdateTvPreferences(bool notifyNewSeasons, bool notifyNewEpisodes, DateTime utcNow)
    {
        if (ContentType != CatalogContentType.Tv)
        {
            throw new InvalidOperationException("Only TV follows support season and episode preferences.");
        }

        NotifyNewSeasons = notifyNewSeasons;
        NotifyNewEpisodes = notifyNewEpisodes;
        UpdatedAt = utcNow;
    }

    public void SetNotifyFromUtc(DateTime notifyFromUtc, DateTime utcNow)
    {
        if (ContentType != CatalogContentType.Tv)
        {
            throw new InvalidOperationException("Only TV follows support notify-from boundaries.");
        }

        if (NotifyFromUtc.HasValue)
        {
            return;
        }

        NotifyFromUtc = notifyFromUtc;
        UpdatedAt = utcNow;
    }

    public void EstablishBaseline(DateTime utcNow)
    {
        if (ContentType != CatalogContentType.Tv)
        {
            throw new InvalidOperationException("Only TV follows support baseline establishment.");
        }

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
}
