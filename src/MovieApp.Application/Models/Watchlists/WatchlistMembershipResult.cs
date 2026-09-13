namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistMembershipResult(
    IReadOnlyList<Guid> WatchlistIds,
    bool IsInWatchlist);
