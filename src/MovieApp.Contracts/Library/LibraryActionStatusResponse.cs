namespace MovieApp.Contracts.Library;

public sealed record LibraryActionStatusResponse(
    string MediaType,
    Guid ContentId,
    bool IsFavorited,
    bool IsInWatchlist,
    IReadOnlyList<Guid> WatchlistIds,
    bool IsFollowing,
    bool NotifyNewSeasons,
    bool NotifyNewEpisodes,
    bool BaselineEstablished,
    bool? IsWatched,
    DateTime? WatchedAt);
