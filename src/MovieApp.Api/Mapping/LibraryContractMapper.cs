using MovieApp.Application.Models.Library;
using MovieApp.Application.Models.Movies;
using MovieApp.Contracts.Library;

namespace MovieApp.Api.Mapping;

public static class LibraryContractMapper
{
    public static LibraryListResponse ToLibraryListResponse(PaginatedResult<LibraryItemResult> result) =>
        new(
            result.Items.Select(ToLibraryItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage,
            result.NextCursor);

    public static LibraryItemResponse ToLibraryItemResponse(LibraryItemResult item) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.PosterUrl,
            item.BackdropUrl,
            item.Year,
            item.VoteAverage,
            item.AddedAt,
            item.WatchedAt,
            item.LastActivityAt,
            item.ProgressPercentage,
            item.NextEpisode is null ? null : ToLibraryNextEpisodeResponse(item.NextEpisode),
            item.CollectionStatus);

    private static LibraryNextEpisodeResponse ToLibraryNextEpisodeResponse(
        LibraryNextEpisodeResult nextEpisode) =>
        new(
            nextEpisode.EpisodeId,
            nextEpisode.SeasonNumber,
            nextEpisode.EpisodeNumber,
            nextEpisode.Title);
}
