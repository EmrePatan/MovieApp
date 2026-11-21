using MovieApp.Domain.Ratings;

namespace MovieApp.Domain.Entities;

public sealed class Rating
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public Guid? EpisodeId { get; set; }

    public int Score { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public Episode? Episode { get; set; }

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
            EpisodeId = null,
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
            EpisodeId = null,
            Score = score,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static Rating CreateForEpisode(Guid userId, Guid episodeId, int score, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (episodeId == Guid.Empty)
        {
            throw new ArgumentException("Episode id is required.", nameof(episodeId));
        }

        RatingScoreRules.Validate(score);

        return new Rating
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = null,
            EpisodeId = episodeId,
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
        var referenceCount = 0;
        if (MovieId is not null)
        {
            referenceCount++;
        }

        if (TvShowId is not null)
        {
            referenceCount++;
        }

        if (EpisodeId is not null)
        {
            referenceCount++;
        }

        if (referenceCount == 0)
        {
            throw new InvalidOperationException("A rating must reference a movie, a TV show, or an episode.");
        }

        if (referenceCount != 1)
        {
            throw new InvalidOperationException(
                "A rating must reference exactly one of a movie, a TV show, or an episode.");
        }

        RatingScoreRules.Validate(Score);
    }
}
