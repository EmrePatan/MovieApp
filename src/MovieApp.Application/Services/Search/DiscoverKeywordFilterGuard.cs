using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class DiscoverKeywordFilterGuard
{
    public static bool IsUnresolvedKeywordFilter(
        IReadOnlyList<Guid> keywordIds,
        IReadOnlyList<int> resolvedTmdbKeywordIds) =>
        keywordIds.Count > 0 && resolvedTmdbKeywordIds.Count == 0;

    public static PaginatedResult<SearchItem> CreateEmptyBrowseResult(int page, int pageSize) =>
        new([], page, pageSize, 0, 0);
}
