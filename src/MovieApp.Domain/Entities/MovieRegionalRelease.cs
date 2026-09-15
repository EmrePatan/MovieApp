using MovieApp.Domain.Enums;

namespace MovieApp.Domain.Entities;

public sealed class MovieRegionalRelease
{
    public Guid MovieId { get; set; }

    public string Region { get; set; } = string.Empty;

    public DateOnly? EffectiveReleaseDate { get; set; }

    public TmdbReleaseType? EffectiveReleaseType { get; set; }

    public string? Certification { get; set; }

    public bool IsFallbackGlobal { get; set; }

    public DateTime SyncedAtUtc { get; set; }

    public Movie Movie { get; set; } = null!;
}
