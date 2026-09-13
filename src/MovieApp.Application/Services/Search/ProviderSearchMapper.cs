using MovieApp.Application.Common;
using MovieApp.Application.Models.Movies;
using MovieApp.Application.Models.Providers;
using MovieApp.Application.Models.Search;

namespace MovieApp.Application.Services.Search;

internal static class ProviderSearchMapper
{
    public static SearchItem ToSearchItem(MovieProviderSummary summary, Guid id) =>
        new(
            id,
            "movie",
            summary.Title,
            null,
            summary.Overview,
            summary.PosterPath,
            null,
            summary.ReleaseDate,
            summary.VoteAverage,
            summary.VoteCount,
            summary.ReleaseDate?.Year);

    public static SearchItem ToSearchItem(TvShowProviderSummary summary, Guid id) =>
        new(
            id,
            "tv",
            summary.Title,
            summary.OriginalTitle,
            summary.Overview,
            summary.PosterPath,
            summary.BackdropPath,
            summary.FirstAirDate,
            summary.VoteAverage,
            summary.VoteCount,
            summary.FirstAirDate?.Year);

    public static SearchSuggestion ToSuggestion(SearchItem item) =>
        new(item.Id, item.Type, item.Title, item.PosterUrl);

    public static PaginatedResult<SearchItem> MergeProviderResults(
        SearchCriteria criteria,
        MovieProviderSearchResult? movieResult,
        TvShowProviderSearchResult? tvResult,
        IReadOnlyDictionary<int, Guid> movieIds,
        IReadOnlyDictionary<int, Guid> tvIds)
    {
        var items = new List<SearchItem>();

        if (movieResult is not null)
        {
            foreach (var summary in movieResult.Results)
            {
                if (summary.TmdbId is null ||
                    !movieIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (tvResult is not null)
        {
            foreach (var summary in tvResult.Results)
            {
                if (summary.TmdbId is null ||
                    !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        var normalizedQuery = string.IsNullOrWhiteSpace(criteria.Query)
            ? null
            : QueryNormalizer.Normalize(criteria.Query);

        var sortedItems = ApplyRelevanceSort(items, normalizedQuery);

        return criteria.Type switch
        {
            SearchContentType.Movie => CreatePaginatedResult(
                sortedItems,
                movieResult?.Page ?? criteria.Page,
                movieResult?.PageSize ?? criteria.PageSize,
                movieResult?.TotalCount ?? 0),
            SearchContentType.Tv => CreatePaginatedResult(
                sortedItems,
                tvResult?.Page ?? criteria.Page,
                tvResult?.PageSize ?? criteria.PageSize,
                tvResult?.TotalCount ?? 0),
            _ => CreatePaginatedResult(
                sortedItems.Take(criteria.PageSize).ToList(),
                criteria.Page,
                criteria.PageSize,
                (movieResult?.TotalCount ?? 0) + (tvResult?.TotalCount ?? 0))
        };
    }

    public static IReadOnlyList<SearchSuggestion> MergeAutocompleteSuggestions(
        string query,
        MovieProviderSearchResult? movieResult,
        TvShowProviderSearchResult? tvResult,
        IReadOnlyDictionary<int, Guid> movieIds,
        IReadOnlyDictionary<int, Guid> tvIds,
        int limit)
    {
        var items = new List<SearchItem>();

        if (movieResult is not null)
        {
            foreach (var summary in movieResult.Results)
            {
                if (summary.TmdbId is null ||
                    !movieIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        if (tvResult is not null)
        {
            foreach (var summary in tvResult.Results)
            {
                if (summary.TmdbId is null ||
                    !tvIds.TryGetValue(summary.TmdbId.Value, out var id))
                {
                    continue;
                }

                items.Add(ToSearchItem(summary, id));
            }
        }

        var normalizedQuery = QueryNormalizer.Normalize(query);

        return ApplyRelevanceSort(items, normalizedQuery)
            .Take(limit)
            .Select(ToSuggestion)
            .ToList();
    }

    private static PaginatedResult<SearchItem> CreatePaginatedResult(
        IReadOnlyList<SearchItem> items,
        int page,
        int pageSize,
        int totalCount)
    {
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedResult<SearchItem>(
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    private static List<SearchItem> ApplyRelevanceSort(
        IReadOnlyList<SearchItem> items,
        string? normalizedQuery)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return items
                .OrderByDescending(item => item.VoteAverage)
                .ThenByDescending(item => item.VoteCount)
                .ToList();
        }

        return items
            .OrderBy(item => ComputeRelevanceRank(item.Title, normalizedQuery))
            .ThenByDescending(item => item.VoteAverage)
            .ThenByDescending(item => item.VoteCount)
            .ToList();
    }

    private static int ComputeRelevanceRank(string title, string normalizedQuery)
    {
        var normalizedTitle = QueryNormalizer.Normalize(title);

        if (string.Equals(normalizedTitle, normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (normalizedTitle.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return 2;
    }
}
