using MovieApp.Application.Models.Watchlists;
using MovieApp.Contracts.Watchlists;

namespace MovieApp.Api.Mapping;

public static class WatchlistContractMapper
{
    public static Application.Models.Watchlists.CreateWatchlistRequest ToCreateWatchlistRequest(
        Contracts.Watchlists.CreateWatchlistRequest request) =>
        new(request.Name);

    public static WatchlistSummaryResponse ToSummaryResponse(WatchlistSummaryResult result) =>
        new(
            result.Id,
            result.Name,
            result.CreatedAt,
            result.UpdatedAt,
            result.ItemCount);

    public static WatchlistDetailResponse ToDetailResponse(WatchlistDetailResult result) =>
        new(
            result.Id,
            result.Name,
            result.CreatedAt,
            result.UpdatedAt,
            result.Movies.Select(ToMovieItemResponse).ToList(),
            result.TvShows.Select(ToTvShowItemResponse).ToList());

    public static WatchlistItemsResponse ToItemsResponse(WatchlistItemsResult result) =>
        new(
            result.Movies.Select(ToMovieItemResponse).ToList(),
            result.TvShows.Select(ToTvShowItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    private static WatchlistMovieItemResponse ToMovieItemResponse(WatchlistItemMovieResult result) =>
        new(
            result.Id,
            result.Title,
            result.PosterPath,
            result.ReleaseDate,
            result.VoteAverage,
            result.CreatedAt);

    private static WatchlistTvShowItemResponse ToTvShowItemResponse(WatchlistItemTvShowResult result) =>
        new(
            result.Id,
            result.Title,
            result.PosterPath,
            result.FirstAirDate,
            result.VoteAverage,
            result.CreatedAt);

    public static WatchlistMembershipResponse ToMembershipResponse(WatchlistMembershipResult result) =>
        new(result.WatchlistIds, result.IsInWatchlist);
}
