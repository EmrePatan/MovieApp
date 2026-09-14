using MovieApp.Application.Models.CatalogFollows;
using MovieApp.Contracts.CatalogFollows;
using MovieApp.Domain.Enums;

namespace MovieApp.Api.Mapping;

public static class CatalogFollowContractMapper
{
    public static CatalogFollowsResponse ToFollowsResponse(CatalogFollowsListResult result) =>
        new(
            result.Items.Select(ToFollowItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

    public static CatalogUpcomingResponse ToUpcomingResponse(CatalogUpcomingListResult result) =>
        new(
            result.Items.Select(ToUpcomingItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);

    private static CatalogFollowItemResponse ToFollowItemResponse(CatalogFollowItemResult item) =>
        new(
            item.ContentId,
            ToContentTypeString(item.ContentType),
            item.Title,
            item.PosterPath,
            item.ReleaseDate,
            item.NotifyMovieRelease,
            item.NotifyNewSeasons,
            item.NotifyNewEpisodes,
            item.BaselineEstablished,
            item.FollowedAt);

    private static CatalogUpcomingItemResponse ToUpcomingItemResponse(CatalogUpcomingItemResult item) =>
        new(
            item.ContentId,
            ToContentTypeString(item.ContentType),
            item.Title,
            item.PosterPath,
            item.ReleaseDate,
            item.IsFollowed);

    private static string ToContentTypeString(CatalogContentType contentType) =>
        contentType switch
        {
            CatalogContentType.Movie => "Movie",
            CatalogContentType.Tv => "Tv",
            _ => contentType.ToString()
        };
}
