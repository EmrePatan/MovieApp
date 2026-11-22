namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistSummaryResult(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ItemCount,
    IReadOnlyList<string> CoverPosterPaths,
    string? Description = null);