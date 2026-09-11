namespace MovieApp.Domain.Entities;

public sealed class WatchedMovie
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid MovieId { get; set; }

    public DateTime WatchedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public Movie Movie { get; set; } = null!;

    public static WatchedMovie Create(Guid userId, Guid movieId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("Movie id is required.", nameof(movieId));
        }

        return new WatchedMovie
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MovieId = movieId,
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
