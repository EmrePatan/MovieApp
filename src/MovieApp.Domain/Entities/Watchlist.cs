using MovieApp.Domain.Watchlists;

namespace MovieApp.Domain.Entities;

public sealed class Watchlist
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Longest list description accepted. Shown on the list page and on public share pages.
    /// </summary>
    public const int MaxDescriptionLength = 280;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;

    public ICollection<WatchlistItem> Items { get; set; } = [];

    /// <summary>
    /// Trims the description and maps blank input to null. Throws when it exceeds <see cref="MaxDescriptionLength"/>.
    /// </summary>
    public static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > MaxDescriptionLength)
        {
            throw new ArgumentException(
                $"Watchlist description must not exceed {MaxDescriptionLength} characters.",
                nameof(description));
        }

        return trimmed;
    }

    public static Watchlist Create(Guid userId, string name, DateTime utcNow, string? description = null)
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
            Description = NormalizeDescription(description),
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

    public void SetDescription(string? description, DateTime utcNow)
    {
        Description = NormalizeDescription(description);
        Touch(utcNow);
    }

    public void Touch(DateTime utcNow) => UpdatedAt = utcNow;
}
