using MovieApp.Domain.Ratings;

namespace MovieApp.Domain.Entities;

public sealed class Rating
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public int Score { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public static Rating CreateForMovie(Guid userId, Guid movieId, int score, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        RatingScoreRules.Validate(score);

        return new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            TvShowId = null,
            Score = score,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static Rating CreateForTvShow(Guid userId, Guid tvShowId, int score, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        RatingScoreRules.Validate(score);

        return new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = tvShowId,
            Score = score,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdateScore(int score, DateTime utcNow)
    {
        RatingScoreRules.Validate(score);
        Score = score;
        UpdatedAt = utcNow;
    }

    public void ValidateInvariants()
    {
        if (MovieId is null && TvShowId is null)
        {
            throw new InvalidOperationException("A rating must reference a movie or a TV show.");
        }

        if (MovieId is not null && TvShowId is not null)
        {
            throw new InvalidOperationException("A rating cannot reference both a movie and a TV show.");
        }

        RatingScoreRules.Validate(Score);
    }
}
