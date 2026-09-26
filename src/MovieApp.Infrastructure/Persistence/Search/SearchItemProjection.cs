namespace MovieApp.Infrastructure.Persistence.Search;

internal sealed class SearchItemProjection
{
    public Guid Id { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string? OriginalTitle { get; init; }

    public string? Overview { get; init; }

    public string? PosterUrl { get; init; }

    public string? BackdropUrl { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    public decimal VoteAverage { get; init; }

    public int VoteCount { get; init; }

    public int? Year { get; init; }

    public int? TmdbId { get; init; }

    public string? KnownForDepartment { get; init; }

    public int RelevanceTier { get; init; }
}
