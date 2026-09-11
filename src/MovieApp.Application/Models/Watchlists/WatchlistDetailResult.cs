namespace MovieApp.Application.Models.Watchlists;

public sealed record WatchlistDetailResult(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WatchlistItemMovieResult> Movies,
    IReadOnlyList<WatchlistItemTvShowResult> TvShows);
