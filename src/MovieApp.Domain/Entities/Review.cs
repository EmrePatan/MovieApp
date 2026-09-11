using MovieApp.Domain.Reviews;

namespace MovieApp.Domain.Entities;

public sealed class Review
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public static Review CreateForMovie(Guid userId, Guid movieId, string content, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
            TvShowId = null,
            Content = ReviewContentRules.Normalize(content),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static Review CreateForTvShow(Guid userId, Guid tvShowId, string content, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (tvShowId == Guid.Empty)
        {
            throw new ArgumentException("TV show id is required.", nameof(tvShowId));
        }

        return new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = tvShowId,
            Content = ReviewContentRules.Normalize(content),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdateContent(string content, DateTime utcNow)
    {
        Content = ReviewContentRules.Normalize(content);
        UpdatedAt = utcNow;
    }

    public void ValidateInvariants()
    {
        if (MovieId is null && TvShowId is null)
        {
            throw new InvalidOperationException("A review must reference a movie or a TV show.");
        }

        if (MovieId is not null && TvShowId is not null)
        {
            throw new InvalidOperationException("A review cannot reference both a movie and a TV show.");
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new InvalidOperationException("Review content is required.");
        }

        if (Content.Length > ReviewContentRules.MaxLength)
        {
            throw new InvalidOperationException(
                $"Review content must not exceed {ReviewContentRules.MaxLength} characters.");
        }
    }
}
