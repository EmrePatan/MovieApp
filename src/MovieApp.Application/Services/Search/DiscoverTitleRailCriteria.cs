using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

/// <summary>
/// Shared browse criteria for Discover title rails (preview carousels and See All defaults).
/// </summary>
public static class DiscoverTitleRailCriteria
{
    public static DiscoverBrowseCriteria Create(
        DiscoverBrowseMode mode,
        int page,
        int pageSize,
        SearchContentType type = SearchContentType.All,
        DiscoverBrowseSort? sort = null) =>
        new(
            mode,
            type,
            [],
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            sort,
            page,
            pageSize);
}
