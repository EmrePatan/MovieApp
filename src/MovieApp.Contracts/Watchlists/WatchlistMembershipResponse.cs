namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistMembershipResponse(
    IReadOnlyList<Guid> WatchlistIds,
    bool IsInWatchlist);
