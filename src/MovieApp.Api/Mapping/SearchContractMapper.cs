using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;
using MovieApp.Contracts.Search;

namespace MovieApp.Api.Mapping;

public static class SearchContractMapper
{
    public static SearchResponse ToSearchResponse(PaginatedResult<SearchItem> result) =>
        new(
            result.Items.Select(ToSearchItemResponse).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);

    private static SearchItemResponse ToSearchItemResponse(SearchItem item) =>
        new(
            item.Id,
            item.Type,
            item.Title,
            item.OriginalTitle,
            item.Overview,
            item.PosterUrl,
            item.BackdropUrl,
            item.ReleaseDate,
            item.VoteAverage,
            item.VoteCount,
            item.Year,
            item.TmdbId,
            item.KnownForDepartment);

    public static SearchAutocompleteResponse ToAutocompleteResponse(IReadOnlyList<SearchSuggestion> items) =>
        new(items.Select(item => new SearchAutocompleteItemResponse(
            item.Id,
            item.Type,
            item.Title,
            item.PosterUrl,
            item.TmdbId,
            item.KnownForDepartment)).ToList());

    public static SearchHistoryResponse ToSearchHistoryResponse(PaginatedResult<SearchHistoryItem> result) =>
        new(
            result.Items.Select(item => new SearchHistoryItemResponse(item.Id, item.Query, item.SearchedAt)).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages,
            result.HasNextPage,
            result.HasPreviousPage);
}
