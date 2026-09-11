namespace MovieApp.Domain.Entities;

public sealed class WatchlistItem
{
    public Guid Id { get; set; }

    public Guid WatchlistId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Watchlist Watchlist { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public static WatchlistItem CreateForMovie(Guid watchlistId, Guid movieId, DateTime utcNow)
    {
        if (watchlistId == Guid.Empty)
        {
            throw new ArgumentException("Watchlist id is required.", nameof(watchlistId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new WatchlistItem
        {
            Id = Guid.NewGuid(),
            WatchlistId = watchlistId,
            MovieId = movieId,
            TvShowId = null,
            CreatedAt = utcNow
        };
    }

    public static WatchlistItem CreateForTvShow(Guid watchlistId, Guid tvShowId, DateTime utcNow)
    {
        if (watchlistId == Guid.Empty)
        {
            throw new ArgumentException("Watchlist id is required.", nameof(watchlistId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        return new WatchlistItem
        {
            Id = Guid.NewGuid(),
            WatchlistId = watchlistId,
            MovieId = null,
            TvShowId = tvShowId,
            CreatedAt = utcNow
        };
    }

    public void ValidateInvariants()
    {
        if (MovieId is null && TvShowId is null)
        {
            throw new InvalidOperationException("A watchlist item must reference a movie or a TV show.");
        }

        if (MovieId is not null && TvShowId is not null)
        {
            throw new InvalidOperationException("A watchlist item cannot reference both a movie and a TV show.");
        }
    }
}
