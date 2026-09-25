namespace MovieApp.Application.Models.Library;

public sealed record LibraryActionSnapshot(
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
