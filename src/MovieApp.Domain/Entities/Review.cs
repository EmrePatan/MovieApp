using MovieApp.Domain.Reviews;

namespace MovieApp.Domain.Entities;

public sealed class Review
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? MovieId { get; set; }

    public Guid? TvShowId { get; set; }

    public Guid? EpisodeId { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? AuthoringLocale { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie? Movie { get; set; }

    public TvShow? TvShow { get; set; }

    public Episode? Episode { get; set; }

    public static Review CreateForMovie(
        Guid userId,
        Guid movieId,
        string content,
        string? authoringLocale,
        DateTime utcNow)
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
            EpisodeId = null,
            Content = ReviewContentRules.Normalize(content),
            AuthoringLocale = authoringLocale,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static Review CreateForTvShow(
        Guid userId,
        Guid tvShowId,
        string content,
        string? authoringLocale,
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

        return new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = tvShowId,
            EpisodeId = null,
            Content = ReviewContentRules.Normalize(content),
            AuthoringLocale = authoringLocale,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public static Review CreateForEpisode(
        Guid userId,
        Guid episodeId,
        string content,
        string? authoringLocale,
        DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (episodeId == Guid.Empty)
        {
            throw new ArgumentException("Episode id is required.", nameof(episodeId));
        }

        return new Review
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = null,
            TvShowId = null,
            EpisodeId = episodeId,
            Content = ReviewContentRules.Normalize(content),
            AuthoringLocale = authoringLocale,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void UpdateContent(string content, string? authoringLocale, DateTime utcNow)
    {
        Content = ReviewContentRules.Normalize(content);
        AuthoringLocale = authoringLocale;
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
            throw new InvalidOperationException("A review must reference a movie, a TV show, or an episode.");
        }

        if (referenceCount != 1)
        {
            throw new InvalidOperationException(
                "A review must reference exactly one of a movie, a TV show, or an episode.");
        }

        if (string.IsNullOrWhiteSpace(Content))
        {
            throw new InvalidOperationException("Review content is required.");
        }

        if (ReviewContentRules.GetContentLength(Content) > ReviewContentRules.MaxLength)
        {
            throw new InvalidOperationException(
                $"Review content must not exceed {ReviewContentRules.MaxLength} characters.");
        }
    }
}
