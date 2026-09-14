using MovieApp.Application.Models.TvShowFollows;
using MovieApp.Contracts.TvShowFollows;

namespace MovieApp.Api.Mapping;

public static class TvShowFollowContractMapper
{
    public static TvShowFollowStatusResponse ToStatusResponse(TvShowFollowStatusResult result) =>
        new(
            result.IsFollowing,
            result.NotifyNewSeasons,
            result.NotifyNewEpisodes,
            result.BaselineEstablished);

    public static TvShowFollowPreferencesUpdate ToPreferencesUpdate(UpsertTvShowFollowRequest request) =>
        new(request.NotifyNewSeasons, request.NotifyNewEpisodes);

    public static TvShowFollowsResponse ToListResponse(TvShowFollowsListResult result) =>
        new(
            result.TvShows
                .Select(item => new TvShowFollowItemResponse(
                    item.Id,
                    item.Title,
                    item.PosterPath,
                    item.FirstAirDate,
                    item.VoteAverage,
                    item.NotifyNewSeasons,
                    item.NotifyNewEpisodes,
                    item.BaselineEstablished,
                    item.FollowedAt))
                .ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);
}
