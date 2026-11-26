namespace MovieApp.Domain.Entities;

public sealed class WatchedMovie
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid MovieId { get; set; }

    public DateTime WatchedAt { get; set; }

    public Enums.WatchDatePrecision WatchedAtPrecision { get; set; }

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
        // Marking an already watched title again must not overwrite a date the user edited.
        if (WatchedAtPrecision != Enums.WatchDatePrecision.Exact)
        {
            return;
        }

        WatchedAt = utcNow;
        UpdatedAt = utcNow;
    }

    public void SetWatchedDate(DateTime watchedAtUtc, Enums.WatchDatePrecision precision, DateTime utcNow)
    {
        WatchedAt = watchedAtUtc;
        WatchedAtPrecision = precision;
        UpdatedAt = utcNow;
    }
}
