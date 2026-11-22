namespace MovieApp.Domain.Entities;

/// <summary>
/// A "not interested" action on a recommended title. Dismissed titles are excluded from the user's
/// personalized candidates and act as a strong negative taste signal.
/// </summary>
public sealed class RecommendationDismissal
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public static RecommendationDismissal CreateForMovie(Guid userId, Guid movieId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new RecommendationDismissal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            TvShowId = null,
            CreatedAt = utcNow
        };
    }

    public static RecommendationDismissal CreateForTvShow(Guid userId, Guid tvShowId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        return new RecommendationDismissal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = tvShowId,
            CreatedAt = utcNow
        };
    }
}
