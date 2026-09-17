using MovieApp.Domain.Watchlists;

namespace MovieApp.Domain.Entities;

public sealed class Watchlist
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<WatchlistItem> Items { get; set; } = [];

    public static Watchlist Create(Guid userId, string name, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > WatchlistNameNormalizer.MaxLength)
        {
            throw new ArgumentException(
                $"Watchlist name must not exceed {WatchlistNameNormalizer.MaxLength} characters.",
                nameof(name));
        }

        return new Watchlist
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            NormalizedName = WatchlistNameNormalizer.Normalize(trimmedName),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    public void Rename(string name, DateTime utcNow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > WatchlistNameNormalizer.MaxLength)
        {
            throw new ArgumentException(
                $"Watchlist name must not exceed {WatchlistNameNormalizer.MaxLength} characters.",
                nameof(name));
        }

        Name = trimmedName;
        NormalizedName = WatchlistNameNormalizer.Normalize(trimmedName);
        Touch(utcNow);
    }

    public void Touch(DateTime utcNow) => UpdatedAt = utcNow;
}
