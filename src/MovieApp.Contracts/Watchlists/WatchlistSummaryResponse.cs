namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistSummaryResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ItemCount);
