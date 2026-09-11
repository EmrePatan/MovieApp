using MovieApp.Application.Models.Watchlists;
using MovieApp.Domain.Entities;

namespace MovieApp.Application.Mapping;

public static class WatchlistMapper
{
    public static WatchlistSummaryResult ToSummaryResult(Watchlist watchlist, int itemCount) =>
        new(
            watchlist.Id,
            watchlist.Name,
            watchlist.CreatedAt,
            watchlist.UpdatedAt,
            itemCount);

    public static WatchlistItemMovieResult ToItemMovieResult(WatchlistItem item) =>
        new(
            item.Movie!.Id,
            item.Movie.Title,
            item.Movie.PosterPath,
            item.Movie.ReleaseDate,
            item.Movie.VoteAverage,
            item.CreatedAt);

    public static WatchlistItemTvShowResult ToItemTvShowResult(WatchlistItem item) =>
        new(
            item.TvShow!.Id,
            item.TvShow.Title,
            item.TvShow.PosterPath,
            item.TvShow.FirstAirDate,
            item.TvShow.VoteAverage,
            item.CreatedAt);

    public static WatchlistDetailResult ToDetailResult(
        Watchlist watchlist,
        IReadOnlyList<WatchlistItem> items)
    {
        var movies = items
            .Where(item => item.Movie is not null)
            .Select(ToItemMovieResult)
            .ToList();

        var tvShows = items
            .Where(item => item.TvShow is not null)
            .Select(ToItemTvShowResult)
            .ToList();

        return new WatchlistDetailResult(
            watchlist.Id,
            watchlist.Name,
            watchlist.CreatedAt,
            watchlist.UpdatedAt,
            movies,
            tvShows);
    }

    public static WatchlistItemsResult ToItemsResult(
        IReadOnlyList<WatchlistItem> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        var movies = items
            .Where(item => item.Movie is not null)
            .Select(ToItemMovieResult)
            .ToList();

        var tvShows = items
            .Where(item => item.TvShow is not null)
            .Select(ToItemTvShowResult)
            .ToList();

        return new WatchlistItemsResult(movies, tvShows, page, pageSize, totalCount, totalPages);
    }
}
