namespace MovieApp.Infrastructure.Persistence.Search;

internal sealed class RankedSearchItemProjection
{
    public SearchItemProjection Item { get; init; } = null!;

    public decimal WeightedRating { get; init; }
}

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
}
