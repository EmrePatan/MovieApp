namespace MovieApp.Contracts.Watchlists;

public sealed record WatchlistDetailResponse(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WatchlistMovieItemResponse> Movies,
    IReadOnlyList<WatchlistTvShowItemResponse> TvShows);
