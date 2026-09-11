namespace MovieApp.Domain.Entities;

public sealed class Favorite
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public static Favorite CreateForMovie(Guid userId, Guid movieId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            TvShowId = null,
            CreatedAt = utcNow
        };
    }

    public static Favorite CreateForTvShow(Guid userId, Guid tvShowId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        return new Favorite
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = tvShowId,
            CreatedAt = utcNow
        };
    }

    public void ValidateInvariants()
    {
        if (MovieId is null && TvShowId is null)
        {
            throw new InvalidOperationException("A favorite must reference a movie or a TV show.");
        }

        if (MovieId is not null && TvShowId is not null)
        {
            throw new InvalidOperationException("A favorite cannot reference both a movie and a TV show.");
        }
    }
}
